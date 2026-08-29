# ADR-007：受控项目写入（AI Recipe 经受限构建路径写入 Unity 项目）

状态：`ACCEPTED`（任务卡 R4 交付物；2026-08-29 独立审计 PASS——无阻塞、6 条建议已在 v1.1 落实——后由主 agent 验收接受；作为 F2「受限构建执行」的规范输入）
日期：2026-08-29
规范架构令牌：`CONTROLLED_PROJECT_MUTATION_V1`

本 ADR 是本产品第一个允许"向 Unity 项目写入"的架构决策。它兼容并继承 ADR-005（`USER_MODE_LOCAL_CREATIVE_TOOL_V1`，普通用户 Broker/Worker 只读架构）与 ADR-006（`AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`，AI 双通道零 fallback），不推翻、不重开任何 U0–U6 / A0–A6 已关闭结论。按 `docs/PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md` 第 17 节的新里程碑规则，"写入 Unity 项目"必须作为单独 mutation 里程碑且安全设计先行——本 ADR 即该里程碑的安全设计文档。

## 1. 背景

REQ-001（`docs/requirements/REQ-001_CHAT_TO_VFX.md`）定义了"对话生成单个特效"的产品需求：AI 生成 Recipe 草稿 → L1/L2 校验 → 用户显式确认 → 受限构建产出 Prefab。其中"构建写入 Unity 项目"是缺口 G-9，写入安全细则由本 ADR 裁决。

裁决基于以下经核实的代码事实（2026-08-29 master）：

1. **写入根**：`VfxCompiler.GeneratedRoot`、`Impact2DCompiler.GeneratedRoot`、`Area2DCompiler` 的写入根同为 `Assets/VFX/Generated`；`VfxCompiler` 以 `IsGeneratedPath`（ordinal 前缀匹配 + 拒绝 `..`）守卫输出路径，越界报 `E600` 并 Blocked；生成产物的 Assets 依赖闭包被 `E601` 检查限制在 `Assets/VFX/Generated/` 与 `Assets/VFX/Templates/` 之内（`Assets/VFX/Shared/**` 依赖会被拒绝）。
2. **审计元数据**：构建流水除资产产物外，还经 `VfxProductionRules.EnforceAndWriteManifest` / `W24S5ProductionGate.CommitFormalManifest` 写权威所有权 manifest 到 `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json`（`VfxProjectRules.RelativeManifestRoot`）；该路径同时是 ADR-005 Worker 只读白名单成员。任何"唯一写入根是 Generated"的绝对表述都与代码不符，本 ADR 如实枚举。
3. **共享资产写入**：`Impact2DSharedLibrary.Ensure()` 与 `Area2DSharedLibrary.Ensure()` 的写入仅限 `Assets/VFX/Shared/<Family>` 子树（Frost/Fire；`VfxStyleSharedLibrary` 写 `Shared/Styles`）；`Assets/VFX/Shared/Shaders` 下的 shader 是仓库预置源资产，`Ensure()` 只经 `Shader.Find` 引用、缺失即抛异常，不写入（只读依赖）。族子树内的写入语义不是"补齐缺失"而是"强制收敛到编译器内置的规范状态"：`CreateProceduralMaterial`/`CreateMaterial` 用 clean 序列化副本**覆盖已存在的材质**，`CreateRingMesh`/`CreateOrUpdateRingMesh` 无条件重算网格内容，`ConfigureTexture`/`ConfigureMaskAtlas` 重写纹理导入设置（纹理源文件缺失则抛异常，Ensure 本就要求人工预置源文件）。`Impact2DCompiler.Build` 与 `Area2DCompiler.Build` 均无条件调用 `Ensure()`（失败分别映射 `E1610`/`E1710`）。
4. **原子与回滚**：`VfxCompiler` 已实现"Generated 下随机命名临时目录（`vfxs6tmp_*`）构建 → 校验 → 备份既有产物 → 提交 → 失败按备份恢复"的序列；manifest 写入使用 `.pending` 文件 + `File.Replace` 有界重试的单文件原子替换；`MatchesExactPlan` 阻止生产门禁通过后替换 recipe/catalog/输出身份（`E24S501`）。
5. **执行工具**：`tools/Invoke-Unity.ps1` 已具备 batchmode 进程纪律：检测本项目的活动 Unity 进程（检测到即 exit 73，不抢锁）、超时只终止经 StartTime 核验的自有 PID（exit 124）；当前 ValidateSet 为 Compile/EditMode/PlayMode/ValidateResults 四模式（`ValidateResults` 不启动 Unity，仅校验 NUnit 结果 XML），均无构建入口。
6. **命令合同**：`src/VFXComposer.Protocol/Commands/` 下的 `ValidateRecipeCommand`、`BuildCandidateCommand`、`ApplyPatchCommand` 只携带哈希身份，明确"raw recipe JSON is never a formal ticket"、"without recipe bytes or an output location"；Broker/Worker 侧无这些命令的执行实现。

REQ-001 §9 与本 ADR 直接相关的风险：R-1（写入根表述不一致）、R-2（Shared 写入突破 Generated）、R-6（生产闸取舍）、R-7（自动重试与零自动网络的边界）、R-9（Unity 单实例锁）。

## 2. 决策

### 2.1 写入面：封闭三成员清单，资产产物唯一根为 `Assets/VFX/Generated`（裁决 a；v1.2 增补成员 3）

AI 触发的构建任务对 Unity 项目目录（`project/`）的全部合法写入是一个**封闭枚举清单**，有且仅有三个成员：

1. **资产产物唯一根 `Assets/VFX/Generated/**`**（与 `VfxCompiler.GeneratedRoot` 常量逐字一致）：最终 Prefab、克隆材质、`BuildManifest.json`、构建期临时目录（§2.4）全部落在此根之下。
2. **审计元数据单点 `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json`**：既有编译器的权威所有权 manifest，非资产、产品自有命名空间、单文件原子替换。它与 Worker 只读白名单共享同一路径，原子替换保证读侧永远只见旧或新完整版本。
3. **构建溯源单文件 `Assets/VFX/Recipes/<Sanitize(effectId)>.json`**（v1.2 增补，据 F2 停手报告裁决）：用户已确认、哈希已复验的 recipe JSON 在构建流程内落盘为 strict 溯源输入，满足 `VfxProductionRules` 的 E8014 强制要求（strict 产出必须能在 `Assets/VFX/Recipes/**` 按规范化哈希溯源到 recipe 源）。约束：只允许 batchmode 构建入口在哈希复验通过之后写入（Desktop 依旧零直接项目 I/O）；单文件原子替换（`.pending` + `File.Replace` 同款纪律）；文件名经 Sanitize + containment 双层守卫并显式拒绝 Windows 保留设备名；**未经用户确认的草稿永不落盘到此路径**。增补理由：不增补则任何新 AI 特效都必须人工登记 `legacyEffectIds`（写 `ProjectSettings/VFXComposer/VfxProjectRules.json`，同样越界且使 strict 溯源形同虚设）——历史上 cohort-i/k 的 AI recipe 正是靠该人工登记绕行的；成员 3 使 strict 溯源对 AI 产物诚实成立。

清单之外的任何显式写入意图一律禁止：`Assets/VFX/Templates/**`、`Assets/VFX/Shared/**`（§2.2）、`ProjectSettings/**` 其余路径（含 `VfxProjectRules.json` 的 `legacyEffectIds` 登记）、`Packages/**`、以及项目目录外的任意仓库路径。Unity 编辑器自身对 `Library/**`、`Temp/**` 等缓存目录的写入是编辑器运行的固有副作用，不是产品的显式写入意图，不属于本清单管辖，也不得被解释为写入授权。构建日志、NUnit 结果、**未确认草稿**的输入暂存一律放在项目目录之外（仓库 `test-results/` 或用户应用数据目录），不属于项目写入面；已确认 recipe 的溯源落盘是且仅是成员 3。

越界即 fail-closed，且必须是**双层防御**：

- 编译器层（现状，保持）：`IsGeneratedPath` 不满足 → `E600` Blocked；产物依赖闭包越界 → `E601`。
- 执行器层（F2 新增）：batchmode 构建入口在调用编译器之前独立校验目标路径归属，越界拒绝且不进入编译流程。任何一层拒绝都意味着零写入。

表述更正：主计划（`docs/plans/OPTIMIZATION_MASTER_PLAN.md` §1 与任务卡 R4）中的 "`Assets/Generated`" 为过时表述，一律以代码常量 `Assets/VFX/Generated` 为准（采纳 REQ-001 R-1；主计划文本的更正属主 agent 职责，见 §7）。

### 2.2 `Assets/VFX/Shared` 政策：AI 构建期一律只读，`Ensure()` 定性为人工维护入口（裁决 b）

**决策**：对 AI 触发的构建任务，`Assets/VFX/Shared/**` 一律只读。共享依赖缺失或不完整时构建**直接失败**（fail-closed，稳定错误码，见 §5），不自动补建、不自动修复、不覆盖。`SharedLibrary.Ensure()` 家族（`Impact2DSharedLibrary.Ensure`、`Area2DSharedLibrary.Ensure`、`VfxStyleSharedLibrary.EnsureAll` 及同类）定性为**人工维护入口**：仅允许用户显式的维护动作（编辑器菜单、人工发起的 batchmode 维护任务）调用，不得出现在 AI 受限构建的调用图中。Shared 的初始化与修复责任在人工侧（这与 `Ensure()` 现状一致——纹理源文件与 `Shared/Shaders` 下的预置 shader 本就必须人工预置，`Ensure()` 对二者均为缺失即抛异常的只读依赖，写入仅发生在 `Shared/<Family>` 子树内）。

**理由**：

1. `Ensure()` 的真实语义是"强制收敛到规范状态"而非"幂等补齐"：它会覆盖已存在的材质、重算已存在的网格、重写纹理导入设置（§1 事实 3）。允许 AI 构建触发它，等于允许一次 AI 动作静默改写同族**全部既有特效**的共享视觉——这正是 ADR-001 记录的 S1 隐式传播风险，且违反其共同约束"不在未记录依赖版本的情况下静默改变已批准视觉"。
2. "补齐缺失但禁止覆盖"看似折中，实际是当前代码中不存在的第三种语义：它要求为每类资产定义"存在即合格"的判据，而"存在但内容错误"的共享资产恰是最难排查的故障面，并使 Shared 的最终状态取决于构建的历史顺序。引入它的验证成本高于收益（备选方案见 §3.2）。
3. 首版 F2 的范围是 Recipe v1 域（`VfxCompiler` 所辖），该链路本就不触碰 Shared，且 `E601` 依赖检查已拒绝 Shared 依赖进入生成产物——本裁决对首版 F2 零代码成本。

**对现有编译器代码的影响**：

- `VfxCompiler`（Recipe v1）：无影响。
- `Impact2DCompiler.Build` / `Area2DCompiler.Build`：二者无条件调用 `Ensure()`，因此**在提供"只验证"模式之前不得进入 AI 构建范围**。只验证模式的要求：校验 `Dependencies` 清单内资产全部存在（可扩展为指纹核对），任何缺失/漂移映射稳定错误码失败，全程零写入；AI 路径只允许调用验证模式。这是 AI 构建的范围限制，不更改既有人工工作流与测试对 `Ensure()` 的调用行为。
- `CODING_STANDARDS.md` §3.2 的"裁决前一律视为只读"自本 ADR 接受后更新为长期规则："AI 构建期一律只读"（文本修改属后续任务，见 §7）。

### 2.3 执行路径：独立的短生命 batchmode 执行体，不复用只读链路、不给 Worker 扩权（裁决 c）

**决策**：项目写入的唯一执行体是 **Unity Editor batchmode 短生命进程**（`-batchmode -nographics` + `-executeMethod` 调用枚举清单内的静态构建入口），每个构建任务一个进程，进程退出即释放项目锁。进程编排复用并扩展 `tools/Invoke-Unity.ps1` 的既有纪律（锁检测、超时、只终止自有 PID）；F2 为其新增受控构建模式。触发者只能是用户显式动作（Desktop 确认提交，或将来 REQ-003 队列中用户显式入队的任务）；零后台构建、零定时构建、零自动构建。

**与 ADR-005 的关系（三条边界全部保持）**：

1. **不复用只读链路做写入**：ADR-005 的 Broker/Worker 会话、命令面、只读白名单原样不动。mutation 路径是与之并存的另一条独立、短生命、每任务一次的执行路径。
2. **不给 Worker 加写权限**：长生命只读 Worker 会话不升级为写会话。
3. **Desktop 依旧零直接项目 I/O**：Desktop 只发起任务与展示结果，写入执行体是 batchmode Unity 进程；Unity 仍是 Unity API 与项目内容操作的唯一所有者。

**Protocol 命令面的定位**：`ValidateRecipeCommand`/`BuildCandidateCommand`/`ApplyPatchCommand` 保持"仅哈希身份、无 recipe 字节、无输出位置"的合同性质，可继续作为任务身份与状态查询的语汇；**本 ADR 不将它们激活为写入授权通道**。若未来要经 Broker/Worker 会话执行写入，必须另立 ADR。

**recipe 字节的受控投递**：用户确认后的 recipe JSON 由 Desktop 写入用户应用数据下的构建输入暂存目录（项目目录外）；batchmode 构建入口以"暂存文件路径 + 预期规范化哈希（`RecipeCanonicalizer.ComputeSha256` 同源算法）"为参数，入口自行读取文件、重算哈希，不匹配即拒绝——入口不信任调用方，哈希一致性在执行体内部重建。

**计划-提交一致性**：AI 构建必须使用"提交的就是 DryRun 批准的那份计划"语义（`MatchesExactPlan` 检查 recipeHash/revision/buildHash/输出路径一致，等价于 `BuildProduction(approvedPlan, ...)` 的复核路径）；是走 `DryRunProduction`/`BuildProduction`（含 `W24S5ProductionGateRequest` 构造）还是新增等价的受限入口，是 F2 的实现取舍（REQ-001 R-6，见 §7），但一致性复核本身是本 ADR 的硬性要求。

### 2.4 原子性、回滚与覆盖策略（裁决 d）

**提交语义（将现状规范化为 MUST）**：构建先落在 `Assets/VFX/Generated` 下随机命名的临时目录（`vfxs6tmp_*`/`impacttmp_*`/`areatmp_*` 前缀），产物校验通过后执行"备份既有产物 → 提交替换 → 异常按备份恢复"的序列（`VfxCompiler.Commit` 的 catch 恢复覆盖材质、Prefab、`BuildManifest.json`、production rules manifest 与新建空目录回收）；manifest 类单文件一律 `.pending` + `File.Replace` 有界重试的原子替换。**任何失败下，上一次成功产物不被破坏**（REQ-001-19）。

**进程级中断（杀进程/断电，catch 恢复无法执行）**：产物目录与临时目录物理分离，中断的典型残留是 Generated 下的孤儿临时目录与 `*.pending` 文件。清理策略：**每次受控构建入口在开始前**清扫 Generated 直接子级中匹配已知临时前缀枚举清单（`vfxs6tmp_`、`impacttmp_`、`areatmp_`，随新编译器登记扩展）的孤儿目录及 `*.pending` 残留，并将清理项记入构建日志；清扫不触碰任何非临时命名的目录。诚实边界：多资产提交序列不是跨文件事务，提交中途被杀可能留下"部分提交"产物；该状态在下一次 DryRun 中因 build hash 不匹配表现为 `Update`，由重建收敛修复。本 ADR 承诺的是"单文件原子 + 失败可回滚 + 中断可检测可收敛"，不承诺跨文件事务原子性（见 §6 第 3 条）。

**同名特效重建的覆盖策略**：**覆盖（备份-替换-可回滚）**，即现状语义。理由：recipe `id` 是产物的稳定身份，`Generated/<Sanitize(id)>` 是其唯一目录；覆盖保持 Runtime Prefab GUID 与引用安全（ADR-001 共同约束），并使幂等语义成立（同一草稿重建第二次 DryRun 为 `Unchanged`）；`BuildManifest.json` 记录 recipeHash/buildHash/编译器版本，使每次覆盖可追溯。DryRun 显式区分 `Create`/`Update`/`Unchanged`，用户确认界面必须呈现该状态——对既有产物的覆盖是知情的显式动作。版本化与拒绝被否决（§3.4）。

### 2.5 有界 schema 修复重试不违反"零自动网络"（裁决 e）

**决策**：AI 生成阶段的有界 schema 修复重试（REQ-001-11/12）**不违反** ADR-006 的零自动网络原则，边界如下：

- 零自动网络禁止的是**无显式用户/任务动作的网络**：保存配置、启动、导航、health probe 不得产生请求。它不禁止"一次显式动作内的有界后续请求"。
- 一次显式"生成"动作授权一个**封闭任务预算**：至多 `1 + N` 次请求（N 默认 2，配置上限 ≤ 5），全部发往同一已解析 route（同 profile、model、endpoint、协议），仅由 L1/L2 校验失败触发；预算耗尽即任务 fail-closed，保留最后草稿与完整错误报告。
- **网络类失败（超时、上游拒绝、不可达）不触发自动重试**，直接终止任务，由用户显式重新发起（REQ-001-13）。
- 这是 ADR-006 明文预留的 "Bounded retries may later be added only for the already resolved one route and request" 的第一次落地：重试永不选择不同的 route，失败永不换 profile/model/endpoint/协议/通道。
- 需要明确的是：schema 修复重试的**请求内容**（携带错误报告的修复话术）不同于原请求，但 profile/capability/model/endpoint/protocol 的 route 身份严格不变。本 ADR 将 ADR-006 §2 上述预留解释为覆盖此情形（同一已解析 route 上、同一显式动作预算内、内容可变的有界后续请求），并额外收紧：网络类失败不享受该预留，不自动重试。
- 重试次数与错误码序列必须进入任务时间线（REQ-001-23），使预算可审计。

## 3. 备选方案与否决理由

### 3.1 写入面（对应 §2.1）

- **沿用历史文本 `Assets/Generated`**：否。与代码常量不符，会使 F2 验收标准歧义（REQ-001 R-1）。
- **每模板族一个写入根 / 多根**：否。扩大 containment 审计面，`E600`/`E601` 的单根检查是现成且已被测试覆盖的防线。
- **把 `ProjectSettings/VFXComposer/BuildManifests` 的 manifest 迁入 Generated 以凑成字面上的"唯一根"**：否。会破坏 Worker 只读白名单、W24 S4/S5 的既有审计事实与 `50_MACHINE_ENFORCEMENT` 所依赖的外置所有权 Manifest 设计，收益为零。如实枚举封闭清单比制造一个假的"唯一"更安全。

### 3.2 Shared 政策（对应 §2.2）

- **允许"幂等补齐缺失共享资产、禁止覆盖已存在文件"**：否。这不是 `Ensure()` 的现有语义（覆盖已存在材质/网格是其设计行为），落地需要重写三个 SharedLibrary 并为每类资产发明"存在即合格"判据；"存在但内容错误"的 Shared 资产是最难排查的故障面；且 Shared 终态将取决于哪个构建先运行的历史顺序。
- **允许 AI 构建调用完整 `Ensure()`**：否。一次 AI 动作可静默改写同族全部既有特效的共享视觉，直接重演 ADR-001 记录的 S1 隐式传播风险。
- **构建期对 Shared 做快照-回滚以允许写入**：否。为一个被上两条否决的能力支付跨目录事务成本，不成立。

### 3.3 执行路径（对应 §2.3）

- **给 ADR-005 Worker 只读命令面加写命令**：否。把长生命只读会话升级为写会话，推翻 ADR-005 的最小能力边界；且现有命令 DTO 从设计上不携带 recipe 字节与输出位置，"扩权"实际等于重新设计整条链路。
- **常驻可写 Unity 会话（解决 batchmode 冷启动，REQ-001 R-3/R-9）**：暂缓，非本 ADR 授权。常驻写会话的生命周期、锁持有、崩溃恢复语义远超本 ADR 范围；交互延迟先由 F3 串行队列在调度层缓解，若确需常驻会话必须另立 ADR。
- **Desktop 直接写项目文件**：否。直接违反 ADR-005 "Desktop 零直接项目 I/O"。

### 3.4 覆盖策略（对应 §2.4）

- **版本化（`Generated/<id>@<n>` 或时间戳目录）**：否。目录无界增长；Runtime Prefab GUID 随版本漂移，破坏引用安全与 ADR-001 共同约束；`Unchanged` 幂等语义失效。
- **拒绝同名重建**：否。"改一句描述重新生成同一特效"是核心迭代工作流；DryRun + 确认界面已把 `Update`（覆盖）语义显式呈现给用户，知情覆盖优于强迫用户先手工删除产物。

### 3.5 重试（对应 §2.5）

- **完全禁止自动重试（每次失败都要求用户点击）**：否。把用户变成 JSON 修复的人肉重试器，违背 REQ-001 目标 2；有界、同 route、可审计的预算已消解"自动网络"疑虑。
- **无界或大预算重试**：否。费用与失控风险；上限 ≤ 5 是硬顶。

## 4. 威胁模型影响

本 ADR 新增的信任面变化：**AI 输出内容第一次进入项目写入的输入链**（此前 AI 产物从不触达 Unity 项目），并新增一个持项目锁的写入进程类型。继承 ADR-005 的信任/不设防划分（信任当前登录用户与其显式动作；不防同用户恶意代码、管理员、内核、离线篡改）。新增攻击/故障面逐项裁决：

| 面 | 载体 | 防线 | fail-closed 反应 |
|---|---|---|---|
| 恶意/幻觉 recipe 内容 | AI 输出 JSON | L1 拒绝未知字段/坏枚举/类型不符（不静默忽略）；L2 `RecipeValidator` + registries + `BudgetCalculator` 权威校验；`E601` 依赖闭包检查；AI 输出仅被解析为数据，任何指令性内容不被执行（REQ-001-08） | 校验失败零写入，任务失败并回显精确错误路径 |
| 路径注入（借 `recipe.id` 操纵输出路径） | recipe `id` 字段 | `Sanitize` 白名单字符集（字母/数字/`_`/`-`）→ `IsGeneratedPath`（ordinal 前缀 + 拒绝 `..`）→ `IsExactGeneratedChild` 目录回收判定 → 执行器层独立复查（§2.1 双层防御） | `E600` Blocked，零写入。已知边缘：Windows 保留设备名（如 `con`）可通过字符白名单，现状由 AssetDatabase 目录创建失败兜底；F2 必须补充显式保留名拒绝与对应负向测试 |
| 确认-提交替换（TOCTOU） | 确认后、构建前的草稿篡改 | 用户确认绑定规范化哈希（REQ-001-15）；构建入口重算哈希比对（§2.3）；`MatchesExactPlan` 阻止 post-gate 的 recipe/catalog/输出身份替换 | 哈希或计划不一致即拒绝（`E24S501` 类），零写入 |
| 构建脚本/编辑器代码篡改 | `Invoke-Unity.ps1`、`-executeMethod` 入口、编辑器包源码 | 同用户恶意代码不设防（继承 ADR-005）；意外与流程性漂移靠 release 完整性检查、git 审阅、入口方法封闭枚举（执行器只调用枚举清单内的静态入口）等纪律性控制缓解 | 无运行时脚本完整性证明（见 §6 第 1 条）；枚举外入口不被调用 |
| 并发构建冲突 | 多个写入进程 / 用户开着图形 Editor | **全局单写者**：同一时刻至多一个 mutation batchmode 进程；启动前检测本项目活动 Unity 进程（`Invoke-Unity.ps1` 既有 exit 73 语义），禁止抢锁、禁止终止非自有进程（超时仅终止经 StartTime 核验的自有 PID）；与只读链路的并发无冲突——Worker 白名单文件均为原子替换写入，读侧只见旧或新完整版本 | 锁不可得即显式失败（提示关闭 Unity 编辑器）或按 REQ-003 排队，绝不并发写 |
| 中断与资源耗尽 | 杀进程、断电、磁盘满 | 临时目录与产物目录物理分离；入口前孤儿残留清扫；单文件原子替换；部分提交经 DryRun `Update` 检测并由重建收敛（§2.4） | 中断不破坏上次成功产物；残留可检测、可收敛，不静默 |

## 5. fail-closed 行为汇总

总原则：**任何未知、越界、漂移、不可核验的状态一律拒绝，且拒绝意味着零写入或已回滚**；禁止静默降级、静默修复、静默换路径。

| 条件 | 行为 | 码/出处 |
|---|---|---|
| L1 结构校验失败（未知字段、坏枚举、类型不符、`recipeVersion != 1`） | 拒绝进入 L2，进入有界修复重试或任务失败 | .NET 侧同构报告（F1 登记，与 `ValidationReport` 字段对齐） |
| L2 权威校验失败（模板不存在、参数越界、预算超限、能力/槽位不符） | 拒绝进入确认态，零写入 | `E3xx`（`RecipeValidator` 等） |
| 输出路径越界 / 含 `..` | 构建 Blocked，零写入 | `E600`（`VfxCompiler`）+ 执行器层独立拒绝 |
| 产物依赖闭包越界（Generated 与 Templates 之外的 Assets 依赖，含 Shared） | 构建失败并回滚 | `E601` |
| 共享依赖缺失/不完整（AI 构建期） | 构建失败，不补建不修复 | 族编译器现有 `E1610`/`E1710` 语义收敛为"只验证失败"码（F2 登记） |
| 构建入口哈希与确认哈希不一致 | 拒绝执行，零写入 | F2 登记稳定码（执行器层） |
| 计划-提交身份漂移（recipe/catalog/输出路径） | 拒绝提交 | `E24S501` 类（`MatchesExactPlan`） |
| 生产门禁请求缺失/被拒（若走 `BuildProduction` 路径） | 构建 Blocked | `E24S500` / 门禁 issue 码 |
| 提交序列内任何异常 | 按备份恢复材质/Prefab/manifest/规则 manifest，回收新建空目录 | `VfxCompiler.Commit` catch 序列；`E602` 报告 |
| Unity 项目锁被占用（图形 Editor 开启或他进程持锁） | 显式失败（或按 REQ-003 排队），不抢锁 | `Invoke-Unity.ps1` exit 73 语义 |
| 构建进程超时 | 仅终止经核验的自有 PID，任务失败 | `Invoke-Unity.ps1` exit 124 语义 |
| AI 重试预算耗尽 | 任务失败，保留最后草稿与错误报告，零写入 | REQ-001-12 |
| 网络类失败（超时/上游拒绝/不可达） | 任务终止，不自动重试、不换 route | ADR-006 稳定错误码 |
| ChatLlm 通道未绑定/配置无效 | 网络前 fail-closed | `ChannelUnbound` 等（ADR-006） |

## 6. 不作声明清单

与 ADR-005/006 同一风格，本 ADR 明确**不承诺**以下内容；任何依赖这些声明的说法都是被禁止的：

1. **不防同用户攻击者**：不防已作为同一 Windows 用户运行的恶意代码、本地管理员、内核破坏、离线磁盘篡改、同用户调试/注入，也不防被用户蓄意选择的恶意项目（含被篡改的模板、共享资产与编辑器包代码）。本 ADR 不提供构建脚本或编辑器代码的运行时完整性证明。
2. **不做视觉质量担保**：构建成功仅指"合法 Recipe 产出通过结构校验（`E601` 等）的 Prefab"；是否好看、是否符合美术验收由用户人工判断（REQ-001 非目标 6）。
3. **不承诺跨文件事务原子性**：单文件替换是原子的，失败路径有备份回滚；但多资产提交序列在进程级中断（杀进程/断电）下可能部分提交，承诺的是可检测（DryRun `Update`）与重建收敛，不是事务。
4. **不承诺 Generated 产物免于人工修改**：用户手工改动 Generated 下产物属于其对自己项目的处置权，表现为下次 DryRun 的 `Update`，不视为攻击、不做保护。
5. **不授权任何其他写入路径**：不激活 Protocol 命令为写入通道；不给 Worker 写权限；Desktop 依旧零直接项目 I/O；AI 图像产物依旧不自动写入 Unity/`Assets`/recipe/patch（ADR-006 边界原样保留，显式图像导出属另一个 mutation 里程碑）。
6. **不扩展 Worker 只读白名单**：本 ADR 未变更任何只读边界；若 F1 的模板参数表送达方案需要扩白名单，须另行走边界变更评审。
7. **不覆盖以下范围**：Recipe v2（`S12SlashCompiler`）域的 AI 构建、批量与队列语义（REQ-002/REQ-003）、真实付费 provider 可用性（继承 A5/A6 的 mock/loopback 边界）。
8. **不构成实现验收**：本 ADR 是 F2 的规范输入，不使任何写入能力立即 GO；F2 须按主计划独立交付与验收（含"越界路径被拒绝"的负向测试，CODING_STANDARDS §4.2）。
9. **哈希不是权威**：`BuildManifest` 的 recipeHash/buildHash、清扫日志、构建记录只是完整性与可审计记录，不把本地进程、构建结果或用户确认变成机器权威（继承 ADR-005 "hash/签名不产生 authority"）。

## 7. 生效条件与遗留决策点

**生效条件**：主 agent 已于 2026-08-29 验收本 ADR（独立审计 PASS，状态 `ACCEPTED`），它自此成为 F2 任务卡的规范输入；F2 的验收标准必须逐条覆盖 §5 的 fail-closed 行为（每个写入路径至少一条负向测试）。

**本 ADR 已裁决但需要后续任务落地的事项**：

1. F2：执行器层路径复查、入口哈希复验、孤儿临时目录清扫、Windows 保留名拒绝及其负向测试（§2.1、§2.3、§2.4、§4）。
2. F2（如扩展族编译器范围时）：`Impact2DCompiler`/`Area2DCompiler` 的"只验证"模式（§2.2）。
3. F1：有界重试预算与任务时间线的实现（§2.5）。

**留给主 agent / 用户拍板的遗留决策点**（本 ADR 的 allow-list 不含以下文件，文本性修正由对应责任方完成）：

1. **主计划表述更正**：已完成——主 agent 已将 `OPTIMIZATION_MASTER_PLAN.md` 中的 "`Assets/Generated`" 更正为 `Assets/VFX/Generated`（REQ-001 R-1，本 ADR §2.1 裁决以代码为准）。
2. **CODING_STANDARDS §3.2 文本更新**："`Assets/VFX/Shared` 裁决前一律视为只读"应更新为引用本 ADR 的长期规则。
3. **`docs/rules/README.md` 阅读顺序**：已完成——README 已收录 ADR-005/006/007（阅读顺序第 11–13 条）。
4. **R-6 生产闸取舍**（REQ-001）：**已裁决（v1.2，据 F2 停手报告）**。F2 核实：`BuildProduction` 对 AI recipe 无可达成功路径——formal 分支要求 `docs/` 下已持久化的 design contract/implementation trace（AI recipe 不具备，凑齐等于向 `docs/**` 写入并伪造证据链，W24S5-010/-020 必拒）；legacy 分支是死路（`TryValidateAuthoritativeLegacy` 放行但 `CommitFormalManifest` 第一条即拒绝 `LegacyDevelopment`，E24S5-092），且全仓 `BuildProduction` 零成功调用点。裁决：AI 构建**不走 `W24S5ProductionGate`**，走 legacy `Build` + F2 执行器层实现的"计划绑定提交"（DryRun 产出计划 → 提交前复核 recipeHash/revision/buildHash/输出路径一致，等价 `MatchesExactPlan` 语义），satisfies §2.3 硬性要求；strict 溯源由写入面成员 3 诚实满足。legacy 批准死路本身判定为 W24 既有代码缺陷，**保持现状不修**（其效果是 fail-closed，修复反而会打开一条本 ADR 不需要的提交路径），仅记录在案。
5. **常驻可写 Unity 会话**：batchmode 冷启动（分钟级）对交互体验的影响（REQ-001 R-3/R-9）在本 ADR 中以"短生命进程 + 队列调度缓解"处理；若产品层面最终无法接受延迟，引入常驻写会话需另立 ADR，由用户拍板。
6. **REQ-001 的 `ProjectSettings/**` 只读表述需修正**：REQ-001 §4 非目标 1 与 REQ-001-18 写"`ProjectSettings/**` 对构建任务只读"，与本 ADR §2.1 的封闭写入面清单（含 `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json` 审计元数据单点）矛盾，应修正为"除 BuildManifests 单点外只读"。冲突以本 ADR 为准；主 agent 已并行派发 REQ-001 v0.3 修正。

## 8. 变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.1 | 2026-08-29 | 初版（任务卡 R4 交付） |
| v1.1 | 2026-08-29 | 独立审计 PASS（无阻塞）后按 6 条建议微调并转正为 `ACCEPTED`：修正 `Ensure()` 写入面为 `Shared/<Family>` 子树（`Shared/Shaders` 为只读预置依赖）；补全 `Invoke-Unity.ps1` 四模式事实；加固 §2.5 重试与 ADR-006 预留的解释边界；§7 遗留点 1/3 标记已完成、新增 REQ-001 `ProjectSettings/**` 只读表述矛盾一条。 |
| v1.2 | 2026-08-29 | 据 F2 停手报告（`BuildProduction` 无可达成功路径 + strict E8014 溯源要求与项目外暂存矛盾）由主 agent 裁决修订：§2.1 写入面增补成员 3（`Assets/VFX/Recipes/<Sanitize(effectId)>.json` 构建溯源单文件，仅构建入口在哈希复验后原子写入）；§7 遗留点 4 定版为"legacy `Build` + 执行器层计划绑定提交，不走 W24S5 闸"；legacy 批准死路（E24S5-092）记录为 W24 既有缺陷、保持 fail-closed 不修。 |
