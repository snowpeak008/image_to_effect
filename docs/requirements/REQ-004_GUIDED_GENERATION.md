# REQ-004 引导式生成与多轮精修（Guided Generation）产品需求

> 状态：ACCEPTED（主 agent 验收，2026-08-31；O-3/O-4 已裁决关闭，RG-6 冲突行为定义归 F8b2 任务卡）｜任务卡：R5（`docs/plans/OPTIMIZATION_MASTER_PLAN.md` §7）｜创建：2026-08-31｜作者：需求文档子 agent
>
> 权威输入：主计划 **§7「追加需求三：生成体验大改版」**（评审事实校正 4 条 + 主 agent 裁决 7 条 + 任务卡表）。裁决已定版，本文是把裁决展开为**可测试需求**，不重开设计；本文与主计划 §7 冲突时以主计划为准。
>
> 边界继承：`docs/rules/ADR-005`（Desktop 零直接项目 I/O）、`ADR-006`（双通道唯一显式绑定、零 fallback、零自动网络）、`ADR-007` v1.2（封闭三成员写入面）与其 **§2.5**（有界修复重试不违反零自动网络）。本文不推翻任何已关闭里程碑，不新增写入面，不引入新网络面。
>
> 与既有 PRD 的分工：
> - **REQ-001**：一次显式生成动作 → 一份 Recipe 草稿 → 确认 → 构建。其 §4 非目标 3「单特效、单对话轮次」中"不支持在上一个特效基础上改一改"的部分**由本文取代**（REQ-001 v0.5 已就地标注 superseded-by-REQ-004）；其开放问题 **O-2（草稿保留份数与清理策略）由本文 §7.5 关闭**。REQ-001 的生成链路语义（prompt 构造、L1、1+N 重试、哈希绑定确认、redaction）在本文中原样继承。
> - **REQ-002**：CLI/MCP 批量入口。本文不改其命令面/工具面；交互式精修**不进** CLI/MCP（§4 非目标 3）。
> - **REQ-003**：Jobs 队列与跨入口 store 语义参照。本文的草稿 store 与 job store 是两个独立 store，但遵循同一套纪律（版本化、原子写 + `.bak`、未知版本 fail-closed、载荷可入 store 不可入日志）。
>
> 代码事实基线：master，2026-08-31（F7b 合入后）。本文引用的每条代码事实均在 §14 映射表给出路径。

---

## 1. 背景与事实底座

用户反馈：Create 页的自由文本输入不友好——用户不知道能说什么、说了也不知道为什么失败、生成一版不满意只能从头再来。主 agent 起草双模式方案后派独立评审（Opus 5），评审 PASS 骨架并纠正了四个事实错误，主 agent 采纳并定案（主计划 §7）。

### 1.1 事实底座（评审校正 4 条，本文全部据此写作）

| 事实 | 内容 | 代码/资产依据 |
|---|---|---|
| F-1 表达空间很小 | 模板目录实为 **6 个模板 / 11 个参数**，全火系（`PFT_2D_Embers`、`PFT_2D_FireCore`、`PFT_2D_FireImpact`、`PFT_2D_FireTrail`、`PFT_2D_LaunchFlash`、`PFT_2D_Shockwave`）；`buildableArchetypes = ["projectile"]`、`buildableDimensions = ["2d"]`；catalog 版本 `1.0.0`、契约修订 `1.4`。strict 结构预算（projectile→simple 档）意味着全 recipe 至多 1~2 个渲染模块、无 `attachTo`、三 stage 根必须齐全，真实形状空间约 20 余种 | `src/VFXComposer.AI.Providers/Recipes/Assets/recipe-v1-template-catalog.snapshot.json` |
| F-2 prompt 参考样例自相矛盾（当前最大失败源） | 快照的 `canonicalExample` 是 `fireball_2d`：8 模块、两处 `attachTo`，靠 `legacyEffectIds` 豁免才合法；照它生成的新 id 必然违反 strict 预算而构建失败。系统 prompt 另有一句 "use exactly three stages" 引导把三段填满 | 同上（`canonicalExample`）；`RecipePromptTemplate.BuildSystemPrompt` |
| F-3 参数上下界从未在 .NET 侧校验 | `MinLiteral`/`MaxLiteral` 的唯一消费者是 prompt 表格渲染（`RenderPromptTable`）；L1 对模块 `parameters` 只校验"是一个对象"，既不校验键集也不校验上下界；越界要到 Unity L2 才发现 | `RecipeTemplateCatalogSnapshot.RenderPromptTable`；`RecipeL1Validator`（模块 `parameters` 仅 `ReadObject(..., required: true)`） |
| F-4 Desktop 无构建面 | Desktop 不引用 `VFXComposer.Batch.Core`；F3b 定版 Desktop 宿主为零 executor 纯观察者。确认草稿后用户必须切到命令行才能构建 | 主计划 F3b 状态板；`apps/VFXComposer.Desktop` 无 Batch.Core 引用 |

F-2 由任务卡 **F8-0** 修正（"F8-0 交付后的 prompt 红线"：strict 合规样例 + 三 stage 根/≤2 渲染模块/禁 attachTo 的明写红线）。本文一切涉及 prompt 内容的需求都以 **F8-0 交付后的 prompt 红线**为前提，不重复定义红线本身。
F-3 由任务卡 **F8a1**（L1.5 目录感知预校验）落地，本文 §9.2 定义其产品语义。
F-4 归任务卡 **R6/F8c**，属本文非目标（§4 第 5 条）。

### 1.2 本文要解决的三个产品问题

1. **不知道能说什么** → 简单模式：示例卡（绑定预置骨架，点卡零 AI 出草稿）+ 能力范围提示 + 建议句（§5.1）。
2. **不满意只能从头再来** → 专业模式：草稿锚定的多轮精修 + 参数面板 + 线性版本链（§5.2、§6、§7、§9）。
3. **AI 反复改坏用户手调的值** → 精修覆盖守卫（§9.3）与艺术家知识章程（§10）。

---

## 2. 术语

| 术语 | 含义 |
|---|---|
| 简单模式 / 专业模式 | 用户可切换的两个生成界面形态（§5）。模式是**呈现与可用动作**的差异，不改变生成链路的契约与预算 |
| 示例卡 | 简单模式的引导卡片，每张绑定一份**预置 recipe 骨架**（入库资产）；点击即产出草稿，零 AI 调用、零网络 |
| 预置骨架 | 语言中立的 recipe JSON 资产（不含展示文案），随程序入库；配套双语展示文案走 Desktop catalog 键 |
| 精修（refine） | 用户对**当前草稿**提出一段自然语言反馈，触发一次 AI 修订，产出该草稿的下一个版本 |
| 一轮精修 | 一次显式用户动作授权的封闭预算：至多 `1 + N` 次请求（§6.2） |
| 锚定三件套 | 一轮精修的上下文闭集：**原始描述 + 当前草稿 + 本轮反馈**（§6.1） |
| 版本链（lineage） | 同一特效草稿的线性版本序列；每条链恰有一个 head（§7.1） |
| origin | 一个版本的产生来源，闭集见 §7.2 |
| 参数面板 | 专业模式中按目录声明的 `[min, max]` 渲染的可编辑参数表（§9） |
| 精修覆盖守卫 | AI 修订落盘前的确定性后处理：对"用户手改过、本轮反馈未点名"的参数自动还原为人改值（§9.3） |
| 艺术家知识片段 | 进入精修 prompt 的版本化英文资产片段：反馈翻译表 + 美学惯例 + 精修纪律（§10） |
| L1 / L2 | 同 REQ-001 §2（.NET 结构校验 / Unity 权威校验） |
| L1.5 | .NET 侧**目录感知**预校验：模板存在性、kind 匹配、参数键集、上下界、strict 结构预算（F8a1）。v1 只作呈现层预警，不改生成判定 |
| 草稿 store | 当前用户应用数据下的草稿持久化文件（三入口共享，§8） |

---

## 3. 目标

1. 用户在**不写任何自由文本**的情况下也能得到一份可确认的草稿（示例卡路径），且该路径零 AI 调用、零网络。
2. 用户对草稿不满意时，能用一句美术语言（"火焰再猛一点、拖尾短一点"）触发一次精修，得到锚定当前草稿的下一个版本，而不是重新抽卡。
3. 用户手动调过的参数不被后续 AI 精修静默改回；被 AI 覆盖的手改一律**可见**。
4. 版本历史线性、可回退、可审计；每个版本知道自己是谁改出来的（`origin` + `parentDraftId`）。
5. 全流程零新网络面、零新写入面、零 route 变更；预算以**请求数**计量（不涉 token），预算与错误码序列可审计。
6. 草稿 store 容量有界且淘汰行为对用户可见，不静默丢失历史。
7. 跨入口（Desktop/CLI/MCP）共享 store 的兼容性显式定版：升版即 fail-closed，不静默迁移、不静默分叉。

---

## 4. 非目标

1. **不做需求规格 IR（中间表示）与两段式拆解调用**（主计划 §7 裁决 1）。理由：在 6 模板 / 11 参数 / 单 archetype 的空间里 IR 与 recipe 同构，成本（新 schema + 校验器 + 确认 UI + 双语键 + 请求翻倍 + ADR-007 改版）不配收益。**重估条件（唯一）**：模板库扩充到多元素族或多 archetype 之后（对应主计划暂缓项 F8b5 与"模板库扩充"track）；在此之前任何"先加个 IR"的提案一律驳回。艺术家知识**不随 IR 一起砍**，它以 prompt 片段形态落地（§10）。
2. **不做开放式对话记忆**：一轮精修的上下文严格是锚定三件套（§6.1），不携带历史轮次的对话消息、不携带其他 lineage 的内容。多轮迭代的连续性由**版本链**承载，不由对话历史承载。
3. **不做 CLI/MCP 交互式精修**：CLI/MCP 保持 REQ-002 的命令面/工具面不变，只能生成与构建，不提供精修/参数面板/版本链操作面。它们**必须**能构建任意 `origin` 的已确认版本（§8）。
4. **不做模板库扩充**：不新增模板、参数、archetype、维度；本文所有"能力范围"表述一律由快照动态派生。
5. **不做 Desktop 内构建面**：确认后如何构建仍是"切命令行"，本文只要求诚实提示（§5.1 第 5 条）。Desktop 构建闭环归 **R6（设计定版）/F8c（实现）**。
6. **不做结构性编辑**：参数面板不能增删 stage/模块、不能改 `templateId`/`kind`/`attachTo`。结构性改动只能由 AI 精修产生（并受 F8-0 的 strict 红线约束）。
7. **不做自动视觉评审**：Prefab 好不好看仍由用户在 Unity 中人工判断（继承 REQ-001 §4 第 6 条）。
8. **不做真实付费 provider 承诺**：验收一律 mock/loopback（继承 REQ-001 §4 第 7 条）。
9. **不做守卫开关**：精修覆盖守卫不可由用户关闭（§9.3），保持行为确定性；用户接受 AI 值的出口是显式动作而非开关。

---

## 5. 双模式定义

### 5.1 简单模式（默认）

面向"不知道能说什么"的用户。界面构成与语义：

1. **示例卡：4~6 张**（下界 4、上界 6；具体张数由 F8a2 按目录实际可表达形状取值）。每张卡绑定一份**预置 recipe 骨架**（入库资产），骨架必须在构建期测试中通过 L1 + L1.5 + F8-0 的 strict 红线。
2. **点卡零 AI**：点击示例卡直接把骨架落成一个新 lineage 的首版草稿（`origin = preset`，§7.2），**不发起任何网络请求**、不构造 prompt、不消耗任何请求预算。
3. **能力范围提示行**：读快照动态渲染（模板数、参数数、`buildableArchetypes`、`buildableDimensions`、catalog 版本、契约修订）。禁止硬编码数字——F-1 的四个错误数字（"12 个模板"等）就是硬编码的产物。
4. **建议句**：把"可以怎么说"的样例句作为可点击项呈现；点击只填入输入框，不触发生成、不触发网络。文案走 Desktop 双语 catalog 键（F7a/F7b 机制），不进 prompt。
5. **诚实提示两条**（文案走 catalog 键）：
   - 能力边界：当前仅支持 2D 火系弹道（由快照派生，不写死"火系"以外的承诺）；
   - 确认后如何构建：给出可复制的命令行 + "构建前需关闭 Unity 编辑器"提醒（F-4 的诚实面，非构建面）。
6. 简单模式**保留**自由描述 + "用 AI 生成"入口，走 REQ-001 既有生成链路（prompt 构造、L1、1+N 修复重试、哈希绑定确认）语义不变。

### 5.2 专业模式

面向"要把它调准"的用户。界面构成：

1. **参数面板**（§9）：按目录 `[min, max]` 渲染当前 head 版本的可编辑参数；手改落新版本，零 AI 成本。
2. **精修输入**（§6）：一段自然语言反馈 + 一次显式"精修"动作 → 锚定当前草稿的一次 AI 修订。
3. **版本链视图**（§7）：线性版本列表，每版显示 `origin`、创建时间、本轮反馈摘要（有界）、守卫还原计数、当前状态；可选中任一版本回退（回退语义见 §7.3）。
4. **L1.5 预警区**：呈现 F8a1 产出的 `issueCode` 对应建议（键→Desktop catalog 文案）。v1 只是预警，不阻断确认（F8a1 定版：不进重试预算、不改 `GenerationService` 判定）。
5. 专业模式**不隐藏**简单模式的 AI 生成入口与示例卡；两模式是同一数据（同一 store、同一 lineage 集合）的两种呈现。

### 5.3 模式偏好持久化（`ui-preferences` schema `/2`）

代码事实（F7a 定版）：`ui-preferences.json` 位于 `%LocalAppData%/VFXComposer/`，schema id 为 `vfxcomposer.ui-preferences/1`，解析纪律为**属性数硬判 + 精确 schema 判等 + 未知属性拒绝**（`UiPreferencesCodec.TryParse`：`properties != 2` 即不可用），不可用时 fail-safe 回退默认值并记 `UI_PREFERENCES_UNUSABLE` 诊断（偏好非安全配置）。

定版规则：

1. schema 升为 `vfxcomposer.ui-preferences/2`，属性闭集为 `{schema, language, generationMode}`（属性数硬判由 2 改为 3）。
2. `generationMode` 的持久化名拼写为闭集字面量 `"Simple"` / `"Professional"`（沿 `UiLanguage` 的既有纪律：持久化名写成常量而非由枚举派生，重命名成员不得静默改变落盘文档）。
3. **必须**兼容读取 `/1`：识别到 `/1` 且恰含 `{schema, language}` 两属性且语言值合法时，**采纳其语言**、`generationMode` 取默认值（Simple），并在下一次显式保存时以 `/2` 重建整个文档。任何情况下不得因升版而静默重置语言偏好。
4. `/2` 文档保持整文档严格判定：任一属性未知、缺失、类型不符或枚举值未知 → 整文档不可用，回退默认（English 或 OS culture 派生，沿 F7a 裁决）并记 `UI_PREFERENCES_UNUSABLE`。理由与备选见 §16 第 4 条。
5. 首次运行（文件不存在）默认 **简单模式**；语言默认沿 F7a 裁决（`zh*` → 中文，否则英文）不变。
6. 模式切换：立即生效 + 立即持久化；**零网络请求**，且不修改任何已存草稿或版本链。

---

## 6. 精修语义

### 6.1 锚定三件套

一轮精修的 AI 上下文是且仅是三件：

| 件 | 内容 | 界 |
|---|---|---|
| 原始描述 | 该 lineage **首版**的用户描述（示例卡起源的 lineage 用该卡的语言中立描述键对应的英文描述）。不可编辑，跨全链不变 | ≤ 16 KiB UTF-8（`RecipeChannelLimits.MaximumDescriptionUtf8Bytes`） |
| 当前草稿 | 当前 head 版本的 recipe JSON（规范化序列化） | ≤ 128 KiB 字符（`RecipeChannelLimits.MaximumDraftJsonCharacters`） |
| 本轮反馈 | 用户本轮输入的一段自然语言 | ≤ 16 KiB UTF-8（复用 `MaximumDescriptionUtf8Bytes` 同一常量） |

- 三件缺一即**拒绝发起**（无网络请求）：无 head 草稿不能精修，空反馈不能精修。
- 不携带历史轮次的对话消息、不携带其他 lineage、不携带任务时间线（§4 非目标 2）。
- prompt 内容侧另加两块**非用户输入**的固定资产：F8-0 交付后的 prompt 红线（strict 约束与合规参考样例）+ 艺术家知识片段（§10）。它们随 `PromptTemplateVersion` 复合版本串记入每个版本（F8b1）。
- 请求界：单条消息 ≤ 16 KiB（`RecipePromptAssembler.MaximumMessageCharacters`，F8b1 合入后原 `RecipePromptTemplate` 的既有纪律由组装器承接；草稿超单消息界时按 F8b1 的按片段边界多消息拆分处理）、消息数 ≤ 64（`ChatChannelLimits.MaximumMessages`——多消息拆分不得越过这条）、整请求 ≤ 256 KiB（`ChatChannelLimits.MaximumRequestBytes`，越界为 `PayloadTooLarge`）。越界一律 fail-closed 映射稳定错误码，**不截断续传**（继承 REQ-001-07）。

### 6.2 每轮预算与显式动作

1. 每轮精修**必须**由一次显式用户动作触发（点击"精修"）。输入反馈、切换版本、切换模式、渲染参数面板、渲染版本链一律零网络。
2. 一轮精修的封闭预算为 **`1 + N` 次请求**，语义与常量沿用 REQ-001-11/12：N 默认 2、配置上限 ≤ 5（`RecipeChannelLimits.DefaultRetryLimit = 2`、`MaximumRetryLimit = 5`）；追加请求**仅由 L1（及 F8a1 交付后的 L1.5，若被接入判定则另行裁决）校验失败触发**，全部发往**同一已解析 route**（同 profile / model / endpoint / 协议），不构成 route 变更。这是 ADR-007 §2.5 已批准语义在精修场景的复用，**不需要 ADR 改版**（砍掉两段式后每轮请求预算不变形）。
3. 预算**按轮独立**：不跨轮累计、不跨轮借用、不因上一轮省下的请求而放宽本轮。
4. 预算耗尽 → 本轮失败：head 不变、**不落新版本**、保留最后一次原始输出与完整错误报告供查看（继承 REQ-001-12 的留存语义）。
5. **网络类失败不自动重试**（超时、上游拒绝、不可达、响应超限）：立即终止本轮，恰 1 次请求，无版本产生，由用户显式重新发起（继承 REQ-001-13 与 ADR-007 §2.5）。
6. 精修轮数本身**不另设硬顶**，由版本链容量上限（§7.5）间接封顶。理由见 §16 第 3 条。

### 6.3 时间线与可观测

每轮精修必须向任务时间线（REQ-001-23 的同一时间线）写入：请求数、每次请求的稳定错误码序列、L1.5 `issueCode` 列表、守卫还原计数与参数路径列表、结果版本 id 与 `origin`、`PromptTemplateVersion` 复合版本串。

时间线内容遵守 REQ-001-16 的 redaction：不含 prompt 原文、AI 原始响应、endpoint、secret。**用户描述与本轮反馈原文允许持久化在草稿 store**（它们是产物上下文，纪律与 REQ-003 §7.1 的载荷规则一致），但不得进入日志、诊断、时间线导出、CLI/MCP 输出与 Jobs 页。

### 6.4 无 IR 拆解段（裁决记录）

v1 的精修是**单段**：一轮 = 至多一个 route 请求 + 预算内的修复重试。不存在"先出规格再出 recipe"的第二段调用。该约束是可测试的（§13 AC-18）。重估条件见 §4 第 1 条。

---

## 7. 版本链

### 7.1 链模型

1. **线性**：每条 lineage 是一个版本序列；每条链恰有一个 **head**；每个版本恰有一个 parent（首版 `parentDraftId = null`）。**不存在分支**。
2. 新版本一律追加在 head 之后并成为新 head。产生新版本的动作恰三类：AI 生成（新链首版）、AI 精修、用户手改；示例卡落骨架产生新链首版。
3. **回退即截断**（§7.3）：回到旧版本会**删除**其后的所有更新版本，不保留被弃分支。
4. 链完整性不变量：任何时刻，链内每条记录的 `parentDraftId` 必为 `null` 或指向**同链内存在的**记录。trim（§7.5）与截断之后该不变量仍须成立。

### 7.2 每版记录字段

每个版本是草稿 store 中的一条记录。在现有 `RecipeDraftRecord` 字段（`draftId`、`status`、`createdUtc`、`updatedUtc`、`correlationId`、`promptTemplateVersion`、`templateCatalogVersion`、`recipeJson`、`canonicalSha256`、`recipeId`、`archetype`、`dimension`、`targetProfile`、`issues`、`requestCount`）之上**新增**：

| 字段 | 语义 |
|---|---|
| `lineageId` | 所属版本链标识；同链全部版本相同 |
| `parentDraftId` | 父版本 `draftId`；首版为 `null` |
| `revisionOrdinal` | 链内序号，首版为 1，链内严格单调递增（截断后不回收已用序号） |
| `origin` | 闭集 `{ preset, ai_draft, ai_refine, human_edit }`（见下方定版说明） |
| `feedbackText` | 本轮反馈原文，仅 `ai_refine` 版本有值；有界 ≤ 16 KiB；受 §6.3 的输出禁令约束 |
| `guardRestorations` | 覆盖守卫在本版本还原的参数清单：参数路径 + 还原采用的人改值来源版本 `draftId`；有界（≤ 64 项，超出记计数并截断清单） |
| `presetId` | 仅 `origin = preset` 的版本有值，指向入库骨架资产标识 |

**`origin` 闭集定版说明（主 agent 已追认，2026-08-31）**：主计划 §7 裁决原写三值 `{ai_draft, ai_refine, human_edit}`；示例卡路径产出的首版既非 AI 生成也非用户手改参数，三值闭集无处安放。**定版为四值，新增 `preset`**——示例卡是零 AI 的确定性产出，与手改在审计语义上必须可区分。见 §17 开放问题 O-4（已关闭）。

### 7.3 回退、确认与哈希绑定

**回退（截断）**：

1. 用户在版本链视图选中版本 `v_k` 并显式确认"回到此版本"→ head 移到 `v_k`，`v_{k+1}..v_n` **立即删除**。
2. 截断必须有一次性显式确认，提示将丢弃的版本数；截断不可撤销。
3. **拒绝条件（fail-closed）**：若 `v_{k+1}..v_n` 中存在状态为 `ConfirmedAwaitingBuild`、`Built` 或 `BuildFailed` 的版本，则截断被拒绝并给出稳定码（F8b2：`TruncationBlocked`）——已确认/已构建的版本是审计记录，不得被删除。用户的出路是另起新链。**澄清（主 agent 裁决，2026-09-02）**：`Superseded` **不在**阻断清单内——失效的确认从未产生构建，用户显式回退时可随其后版本一并删除；它仍受级 1 trim 保护（§7.5 第 4 条），两者不矛盾：trim 是系统自动行为，截断是用户显式动作。

**确认与哈希绑定**（继承 REQ-001-14/15，代码事实：`RecipeDraftStore.Confirm/MarkBuilt/MarkBuildFailed` 经 `Advance` 做 `CanonicalSha256` **精确判等**，不符即 `HashMismatch`）：

4. 确认仍绑定被确认版本的 `CanonicalSha256`，判等规则与实现不变。
5. **人改与 AI 改一律落新版本**，绝不原地改写已存版本的 `recipeJson`/`canonicalSha256`。新版本状态为 `PendingConfirmation`。
6. 落新版本时，若同链存在状态为 `ConfirmedAwaitingBuild` 的版本，该版本**必须**迁移为新终态 **`Superseded`**（"确认已失效，不可再构建"），迁移与其他状态推进同样做哈希绑定。这是 REQ-001-15「确认后草稿变更必须使确认失效」在版本链上的语义延伸：确认的对象是**某一个版本**，而链的 head 已经前移。
7. `Built` / `BuildFailed` / `Failed` 是既有终态，**不被 supersede**（它们是已发生事实的记录）。
8. `Superseded` 版本不得出现在 `ListConfirmedAwaitingBuild()` 的待构建 backlog 中。

### 7.4 store schema 升版（仿 F3c）

代码事实：草稿 store 的 wire 版本是 `RecipeDraftCodec` 写出的整数 `formatVersion`，取自 `AiContractVersions.RecipeDraftRecordFormatVersion`（当前 **1**），读取时做**精确判等**（`version.GetInt32() != ...` 即 `InvalidDataException`）；`RecipeDraftStore.LoadCore` 在 primary 与 backup 都不可解析时抛 `RecipeDraftStoreErrorCode.StorageFailed`。

定版规则：

1. `RecipeDraftRecordFormatVersion` 升为 **2**；精确判等纪律保持；未知版本（含 1）**fail-closed**，不静默迁移、不静默重建、不降级写回版本 1（仿 F3c 先例）。
2. **必须区分"版本不支持"与"文件损坏"**：新增稳定码 `RecipeDraftStoreErrorCode.UnsupportedVersion`。理由：当前实现下版本 1 文件与乱码文件都收敛到 `StorageFailed`，用户无法知道该做的动作是"删除旧文件重建"而不是"报 bug"。这是本文在裁决框架内的细化决策。
3. 旧文件处置行为（明示）：程序**不删除、不改名、不迁移**旧文件。首次遇到版本 1 文件时以 `UnsupportedVersion` fail-closed，并向用户给出精确处置指引——删除当前用户应用数据目录下的 `VFXComposer/AI/recipe-drafts.json` 及其 `.bak` 后重建。指引文案只描述相对位置，不拼接绝对路径（继承 §6.3 与 REQ-003 §9 第 9 条的 redaction 纪律）。
4. fail-closed 的代价必须显式承认：在用户完成上述一次性删除动作之前，草稿保存/读取/确认全部不可用。这是刻意选择（宁可停摆不可静默丢历史），备选与否决见 §16 第 1 条。
5. **backup 不作版本兜底（F8b2 实现事实，主 agent 确认 2026-09-02）**：primary 命中 `UnsupportedVersion` 时**不查阅** `.bak`（即便其恰为版本 2）；primary 损坏而 `.bak` 为版本 1 时同样报 `UnsupportedVersion`。`.bak` 只兜底"同版本文件损坏"，不兜底"版本不支持"，避免静默恢复出与用户预期不同的记录集。
6. **写入侧上界自检（F8b2）**：持久化前按读取上界自检，超界即以 `StorageFailed` 拒绝写出（原子替换之前），避免写出下次读不回的文件形成自锁；文件保持上一份可读态。

### 7.5 两级容量 cap 与 trim 可见语义（关闭 REQ-001 O-2）

代码事实：当前上限是**单级**的 `RecipeDraftStore.MaximumRetainedRecords = 32` 条记录，按 `UpdatedUtc` 降序保留、其余静默丢弃；读取上界 `MaximumFileBytes` 由 `32 * (128 KiB * 4 + 64 KiB)` 派生（≈ 18 MiB）。单级记录数上限在版本链模型下会立刻失效：一条重度精修链就能吃掉全部 32 条。

定版规则（**本节即 REQ-001 开放问题 O-2 的关闭答案**）：

1. **级 1（每 lineage）**：一条链最多保留 **16 个版本**，且该链全部版本的 `recipeJson` 累计 ≤ **1 MiB**；两者取先到者触发链内 trim。
2. **级 2（全局）**：最多保留 **8 条 lineage**；超出时按"最近活动时间"最久未活动者优先淘汰。**F8b2 收紧（主 agent 裁决，2026-09-02）**：含任一 `ConfirmedAwaitingBuild` 版本的链**免于级 2 淘汰**（它是跨入口待构建 backlog 的条目，Desktop 确认后 CLI/MCP 可能稍后才构建），淘汰候选只在不含待构建版本的链中取最久未活动者；若全部 8 条既有链都含待构建版本，新链创建以 `LineageCapacityExhausted` fail-closed 拒绝且文件不变。仅含 `Built`/`BuildFailed`/`Superseded`/`PendingConfirmation`/`Failed` 的链仍可淘汰（构建事实已在 Unity 项目的 recipe 溯源文件与 manifest 留档），淘汰必须出现在类型化结果中。
3. **淘汰粒度**：级 2 的淘汰单位是**整条链**，不是单条记录。理由：跨链混合淘汰会产生 `parentDraftId` 指向已被 trim 记录的孤儿版本，破坏 §7.1 第 4 条不变量。
4. **受保护记录**：head、以及状态为 `ConfirmedAwaitingBuild` / `Built` / `BuildFailed` / `Superseded` 的记录**不被级 1 trim**。级 1 trim 从"最老且非受保护"的版本开始。
5. **受保护记录占满 cap 时拒绝新版本**（fail-closed）：若一条链的受保护记录已达 16 个版本或 1 MiB，则新版本创建被拒绝并返回稳定码，提示用户另起新链或清理该链。绝不为了腾位置而丢弃审计记录。
6. **trim 语义用户可见**：任何 trim（级 1 淘汰版本、级 2 淘汰整链）与截断都必须产生用户可见记录——Create 页一次性提示 + 任务时间线条目（含淘汰的版本数/链数）。**禁止静默丢弃**（这正是当前 `Persist` 的 `.Take(32)` 行为的产品缺陷）。
7. **读取上界纪律**：升版后 store 文件读取上界**不得超过 32 MiB**。当前 `MaximumFileBytes` 的每记录余量（`128 KiB * 4 + 64 KiB` ≈ 576 KiB）过于悲观，若原样乘以派生记录上界（16 × 8 = 128）会膨胀到 ≈ 72 MiB。实现须改用"每链字节上限（1 MiB）× 链数上限（8）+ 有界元数据余量"派生。可测试：合成一个满额（8 链 × 16 版）store 文件，断言可读且体积在上界内。

**数值理由**：

- **16 版/链**：精修是收敛过程，实测语境（6 模板 / 11 参数）下 3~8 轮即定稿。16 覆盖"AI 出 1 版 + 8 轮精修 + 手改交替"的重度会话，并能在链内保留完整 `origin` 链条供审计。超过 16 通常意味着用户在打转，此时最老的中间版本对回退已无价值。
- **1 MiB/链**：契约上界 `MaximumDraftJsonCharacters = 128 KiB` 是极端值，真实 recipe（如 `batches/recipes/spark_projectile_2d.json`）约 1~2 KiB；1 MiB 相当于 16 个 64 KiB 的怪物版本，实际永不触及，只作膨胀防线。
- **8 条链**：Desktop 是单人单机工具，同时在做的特效数量级为 1~3；8 覆盖"并行探索几个方向 + 保留最近历史"。不取 32 是因为 32 × 16 = 512 条记录会把文件与读取上界推到不可控（见第 7 条）。
- 派生记录上界 **128** 条（16 × 8），是现行 32 的 4 倍，与"每条链是一个多版本对象"的模型相称。

---

## 8. 跨入口约束

代码事实：三个入口共享**同一个** store 文件。`AiDesktopRuntimeFactory.CreateCurrentUser()` 把路径固定为 `<LocalApplicationData>/VFXComposer/AI/recipe-drafts.json`；Desktop 经该工厂构造运行时，CLI 的 `DesktopGenerationRuntime`（`apps/VFXComposer.Cli/CliProductionEnvironment.cs`）调用同一工厂并把 `_runtime.RecipeDrafts` 作为 `DraftStore`，MCP 复用 CLI 的执行层（`VFXComposer.Batch.Core` 的 `RecipeGenerationJobExecutor` / `RecipeBuildOrchestrator`）。

定版规则：

1. 草稿 store 的 schema 升版是**跨入口破坏性变更**。三入口**必须同版本部署**：Desktop、`vfxc`、`vfxc-mcp` 来自同一构建产物集，不得混版运行。
2. 混版运行的行为必须 fail-closed 且可辨识：
   - **旧入口 + 新文件**（`formatVersion = 2`）：旧代码做精确判等 → `InvalidDataException` → primary/backup 均不可解析 → `StorageFailed`。旧代码不可能知道新错误码，如实记录为"旧入口只会给出 `StorageFailed`，无法自我诊断"，这是要求同版本部署的直接理由。
   - **新入口 + 旧文件**（`formatVersion = 1`）：`UnsupportedVersion` + §7.4 第 3 条的处置指引。
   - 两个方向都**不得**静默降级、不得静默重建、不得改写对方版本的文件。
3. 严禁"新旧双文件并存"式规避（例如新版本改用 `recipe-drafts.v2.json` 让旧入口继续读旧文件）：那会造成两套入口各看到一半草稿的**静默分叉**，比停摆更危险。否决记录见 §16 第 1 条。
4. CLI/MCP 的取草稿路径必须按新 schema 回归通过，语义不变：`ListConfirmedAwaitingBuild()` 仍按确认顺序（最早在前）返回待构建草稿；`Confirm`/`MarkBuilt`/`MarkBuildFailed` 的哈希绑定与状态前置条件不变；`Superseded` 记录不进 backlog（§7.3 第 8 条）。
5. CLI/MCP **不得**因 `origin` 值拒绝构建：`preset`、`ai_draft`、`ai_refine`、`human_edit` 四类版本一旦被确认，构建路径一视同仁（非目标 3 只排除交互式精修，不排除构建）。
6. CLI/MCP 侧不新增命令/工具面（REQ-002 的 7 命令 + 8 工具闭集不变）。版本链信息若需在 CLI 可见，另立需求，不在本文。

---

## 9. 参数面板

### 9.1 渲染与编辑

1. 渲染源是**快照声明**：对当前 head 草稿的每个模块，按其 `templateId` 从快照取参数键集、`type`、`MinLiteral`/`DefaultLiteral`/`MaxLiteral` 渲染可编辑控件，并显示 `[min, max]`（这使 F-3 里"上下界只被 prompt 表格消费"的现状终结于 UI 侧）。
2. 只渲染快照声明的键：草稿中出现的未声明键呈现为 L1.5 预警项，不给编辑控件（不鼓励用户维护非法字段）。
3. 面板**不能**增删 stage/模块，不能改 `id`/`kind`/`templateId`/`attachTo`（§4 非目标 6）。
4. 类型纪律：快照 `type = integer` 的参数只接受整数输入；`float` 接受有限实数（拒绝 NaN/Infinity）。

### 9.2 手改的预校验与落盘

1. 手改提交**必须**先过 **L1.5**（F8a1：模板存在性、kind 匹配、参数键集、上下界、strict 结构预算）与 L1 结构校验。
2. 越界/类型不符 → **拒绝提交**，给出精确到参数路径的提示与允许区间。**不夹取（clamp）、不静默纠正、不四舍五入**——静默纠正会让用户以为自己设的值生效了。
3. 校验通过 → 落**新版本**，`origin = human_edit`，状态 `PendingConfirmation`（§7.3 第 5/6 条）。
4. 手改路径**零 AI 请求、零网络**，不消耗任何请求预算。

### 9.3 精修覆盖守卫

**问题**：AI 精修返回的是完整 recipe。用户上一轮手调过 `PFT_2D_FireTrail.width`，本轮只说"火焰再猛一点"，AI 很可能把 `width` 一起写回默认值——用户的调整被静默吞掉。

**定版规则**：

1. **触发点**：AI 精修结果通过 L1 之后、落新版本之前，逐字段 diff。守卫是**同一轮内的确定性后处理**，**不额外产生版本**（还原后的内容就是该 `ai_refine` 版本的内容）。
2. **守卫域**：模块参数标量，路径形如 `stages[<stageId>].modules[<moduleId>].parameters.<name>`。结构性差异（新增/删除模块或 stage、改 `templateId`/`kind`）**不在守卫域**——那是 AI 有权做的结构性修订，只受 F8-0 的 strict 红线约束。
3. **还原条件（三条同时成立才还原）**：
   a. 该参数在当前 head 的**祖先链**上存在 `origin = human_edit` 的设定值（即用户确实手调过它，且该设定值未被后续 `human_edit` 覆盖）；
   b. 本轮反馈**未点名**该参数（判定见第 4 条）；
   c. AI 新值 ≠ 该人改值。
4. **点名判定必须确定性且保守**：以艺术家知识片段（§10）中反馈翻译表提供的**参数别名词表**做 ordinal-ignore-case 词元/子串匹配；**不做语义推断、不调用 AI 判定、不用启发式相似度**。匹配保守 ⇒ 只有明确命中词表才算"点名"；未命中即视为"未点名"，进而还原人改值。这正是"fail-safe 偏向保留人改"的实现：不确定时保住用户的手调。
5. **可见性**：还原清单（参数路径 + 还原前的 AI 值 + 还原采用的人改值）随该版本持久化（`guardRestorations`，§7.2）并在两处可见——确认面板的"已保留你的手动调整：N 项"清单 + 任务时间线条目。禁止静默还原。
6. **用户的出口**：守卫无开关（§4 非目标 9）。用户若确实想采纳 AI 的值，在参数面板把该参数改成 AI 值即可——那会产生一个新的 `human_edit` 版本，也就更新了第 3 条 a 的"人改值"，后续轮次不再还原。
7. **确定性**：同一（parent 链、AI 输出、反馈文本、词表版本）四元组必须产出同一还原清单（快照测试可钉）。

---

## 10. 艺术家知识章程

本节定义**内容要求与治理纪律**，不写具体英文条文（条文本体由 F8b4 依本章程撰写并入库）。

### 10.1 片段必须包含的三段内容

1. **反馈翻译表（美术语言 → 参数动作）**：把用户会说的话映射到"哪个参数、往哪个方向、大约多少幅度"。要求：
   - 覆盖率：目录中**每一个可编辑参数**（当前 11 个）至少有一条词条命中；每条词条给出参数路径族（`templateId.parameter`）、方向（增/减）、幅度语义（小幅/中幅/到界）；
   - 同时提供**别名词表**——即 §9.3 第 4 条点名判定消费的确定性词表（例如 "trail / streak / tail" → `PFT_2D_FireTrail.width|time`）。翻译表与别名词表必须来自同一份资产，避免两处漂移；
   - 只允许落在 `[min, max]` 内的动作；越界动作必须写成"到界即止"。
2. **目录内美学惯例**：当前目录（2D 火系弹道）的搭配与时序惯例，例如：launch/travel/impact 三段的时长量级关系、`LaunchFlash.lifetime` 与 stage duration 的匹配、`Embers.rate` 与 `FireCore.scale` 的观感耦合、strict 预算下"该省哪个模块"的取舍优先级。惯例必须是**目录内可验证**的陈述，不得引入目录外的模板/参数（否则等于让 AI 生成不可构建的 recipe）。
3. **精修纪律**：
   - 只改本轮反馈点名的方面，其余字段逐字保留；
   - 输出完整 recipe JSON 单对象，无 markdown 围栏、无解释（与既有 `RecipePromptAssembler`（原 `RecipePromptTemplate`）的输出指令一致）；
   - 不得改 `id`、`metadata.templateCatalogVersion`、`recipeVersion`；
   - 遵守 F8-0 交付后的 strict 红线（三 stage 根齐全、全 recipe ≤ 2 个渲染模块、禁 `attachTo`）。

### 10.2 语言、落点与演进规则

1. **纯英文**（F7 裁决 2 不动）：片段是 prompt 资产，不进 Desktop 双语 catalog，不含中文，不含 UI 文案键。用户可见的建议文案是另一套东西（catalog 键，§5.1 第 4 条）。
2. **源文档（唯一真源）**：`docs/ai-workflow/refine-artist-knowledge.md`（英文，新建，归 F8b4 交付；不在 R5 的 allow-list 内）。人类在此编辑。
3. **入库资产（程序消费）**：`src/VFXComposer.AI.Providers/Recipes/Assets/refine-artist-knowledge.fragment.json`，embedded resource，携带 `schema` / `version` / `source` / `exportedOn` 头部字段——**仿 catalog snapshot 的 source/re-export 纪律**（现行先例：快照的 `"source": "docs/ai-workflow/template-parameters.generated.md + ... ; re-export after a catalog change"` + `"exportedOn"`）。结构化 JSON 而非 markdown，因为别名词表要被 §9.3 的守卫按结构消费。
4. **演进规则**：
   - 改源文档 → 重新导出片段 → 片段 `version` 递增 → `PromptTemplateVersion` 复合版本串随之变化（F8b1 定版：复合版本写入 `PromptTemplateVersion`，**不触发 store 升版**）→ 更新 prompt 组装快照测试；
   - 片段与源文档不同步（内容哈希不符）即测试失败；
   - **目录变更连锁**：模板或参数增删时，必须在同一轮复核翻译表与别名词表的覆盖率；覆盖缺口即测试失败（构造性遍历快照参数集，不允许人工清单蒙混）；
   - 片段有界：进入 prompt 后仍须满足单消息 ≤ 16 KiB 与整请求 ≤ 256 KiB（§6.1）。

---

## 11. 编号功能需求

每条可独立测试。"必须"为验收硬条件。

### 11.1 双模式（§5）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-01 | 模式为闭集 `{Simple, Professional}`，任意时刻恰一个生效；切换立即生效、立即持久化，且**零网络请求**、不修改任何已存草稿或版本链 | P0 |
| REQ-004-02 | 简单模式提供 4~6 张示例卡，每张绑定一份入库预置骨架；骨架在构建期测试中通过 L1 + L1.5 + F8-0 strict 红线 | P0 |
| REQ-004-03 | 点击示例卡**零 AI、零网络**地产出新 lineage 首版草稿（`origin = preset`），不构造 prompt、不消耗请求预算 | P0 |
| REQ-004-04 | 能力范围提示行的全部数字与枚举由快照动态派生（模板数、参数数、`buildableArchetypes`、`buildableDimensions`、catalog 版本、契约修订）；无硬编码数字，有"改快照即改提示"的测试 | P0 |
| REQ-004-05 | 建议句为可点击项，点击只填输入框；文案走 Desktop 双语 catalog 键；点击零网络 | P1 |
| REQ-004-06 | 简单模式给出"确认后如何构建"的诚实提示（可复制命令 + 关闭 Unity 编辑器提醒）与能力边界提示；两者走 catalog 键 | P1 |
| REQ-004-07 | 专业模式同时提供参数面板、精修输入、版本链视图、L1.5 预警区，且不隐藏 AI 生成入口与示例卡 | P0 |

### 11.2 模式偏好持久化（§5.3）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-08 | `ui-preferences` schema 升 `vfxcomposer.ui-preferences/2`，属性闭集 `{schema, language, generationMode}`（属性数硬判 2→3）；`generationMode` 持久化名为字面量 `"Simple"`/`"Professional"`，不由枚举成员名派生 | P0 |
| REQ-004-09 | **必须**兼容读取 `/1` 文档：采纳其 `language`，`generationMode` 取默认，下次显式保存以 `/2` 重建；升级路径下**语言偏好绝不被静默重置**（专项测试：写 `/1` 中文文档 → 启动 → 断言中文生效 → 保存 → 断言文件为 `/2` 且语言仍为中文） | P0 |
| REQ-004-10 | `/2` 文档任一属性未知/缺失/类型不符/枚举值未知 → 整文档不可用，fail-safe 回退默认并记 `UI_PREFERENCES_UNUSABLE`；文件缺失不记诊断（首次运行属正常） | P0 |
| REQ-004-11 | 首次运行默认简单模式；语言默认沿 F7a 的 OS culture 派生规则不变 | P1 |

### 11.3 精修语义（§6）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-12 | 每轮精修必须由显式用户动作触发；输入反馈、切换版本、切换模式、渲染面板/版本链一律零网络（有零网络测试） | P0 |
| REQ-004-13 | 精修上下文严格为锚定三件套（原始描述 + 当前 head 草稿 + 本轮反馈）+ 固定 prompt 资产；不含历史轮次对话消息、不含其他 lineage。测试：第二轮精修的请求消息中不出现第一轮的反馈文本 | P0 |
| REQ-004-14 | 三件套缺一即拒绝发起且零网络（无 head、空反馈、空原始描述三条负向路径） | P0 |
| REQ-004-15 | 精修走**单 route**：同一 ChatLlm 显式绑定，禁 route 变更与 fallback（ADR-006）；未绑定时在网络前 fail-closed | P0 |
| REQ-004-16 | 每轮预算 `1 + N`（N 默认 2、上限 ≤ 5，沿用 `RecipeChannelLimits` 常量），追加请求仅由校验失败触发；预算按轮独立，不跨轮累计/借用 | P0 |
| REQ-004-17 | 预算耗尽时本轮失败：head 不变、不落新版本、保留最后原始输出与完整错误报告 | P0 |
| REQ-004-18 | 网络类失败不自动重试：恰 1 次请求、无版本产生、稳定错误码呈现、由用户显式重发 | P0 |
| REQ-004-19 | 反馈 ≤ 16 KiB UTF-8（`MaximumDescriptionUtf8Bytes`）；单消息 ≤ 16 KiB；整请求 ≤ 256 KiB；越界 fail-closed 映射稳定码，不截断续传 | P0 |
| REQ-004-20 | 每轮向任务时间线写入请求数、错误码序列、L1.5 issue 码、守卫还原计数与路径、结果版本 id/origin、`PromptTemplateVersion` 复合版本串；时间线不含 prompt 原文、AI 原始响应、endpoint、secret | P0 |
| REQ-004-21 | 反馈原文与原始描述可持久化在草稿 store，但不得进入日志、诊断、时间线导出、CLI/MCP 输出、Jobs 页（有泄露负向测试） | P0 |
| REQ-004-22 | 一轮精修至多一个 route 请求 + 预算内修复重试；不存在"先出规格再出 recipe"的第二段调用（断言请求数上界与消息构成） | P0 |

### 11.4 版本链（§7）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-23 | 链为线性：每链恰一个 head、每版恰一个 parent（首版 `null`）、无分支；`revisionOrdinal` 链内严格单调递增且截断后不回收 | P0 |
| REQ-004-24 | 每版持久化 `lineageId`、`parentDraftId`、`revisionOrdinal`、`origin`（闭集，§7.2）、`feedbackText`（仅 `ai_refine`）、`guardRestorations`、`presetId`（仅 `preset`）；未知 `origin` 值读取即 fail-closed | P0 |
| REQ-004-25 | 回退 = 截断：head 移到目标版本、其后版本立即删除，须一次性显式确认并提示丢弃版本数；截断不可撤销 | P0 |
| REQ-004-26 | 截断范围内存在 `ConfirmedAwaitingBuild`/`Built`/`BuildFailed` 版本时，截断被拒绝并给出稳定码（审计记录不可删） | P0 |
| REQ-004-27 | 人改与 AI 改一律落新版本，绝不原地改写已存版本的 `recipeJson`/`canonicalSha256`；新版本状态 `PendingConfirmation` | P0 |
| REQ-004-28 | 落新版本时同链的 `ConfirmedAwaitingBuild` 版本必须迁移为新终态 `Superseded`（哈希绑定）；`Built`/`BuildFailed`/`Failed` 不被 supersede；`Superseded` 不进 `ListConfirmedAwaitingBuild()` backlog | P0 |
| REQ-004-29 | 草稿 store `formatVersion` 升 2，精确判等保持，未知版本 fail-closed；不静默迁移、不重建、不降级写回 | P0 |
| REQ-004-30 | 新增稳定码 `UnsupportedVersion`，与 `StorageFailed`（损坏）严格区分；旧文件不被删除/改名/迁移；处置指引只描述相对位置不拼绝对路径 | P0 |
| REQ-004-31 | 两级 cap：每 lineage ≤ 16 版且累计 `recipeJson` ≤ 1 MiB；全局 ≤ 8 条 lineage，超出按最久未活动淘汰**整条链** | P0 |
| REQ-004-32 | 受保护记录（head、`ConfirmedAwaitingBuild`/`Built`/`BuildFailed`/`Superseded`）不被级 1 trim；受保护记录占满 cap 时**拒绝新版本**并给稳定码，绝不丢弃审计记录 | P0 |
| REQ-004-33 | 一切 trim 与截断对用户可见（Create 页提示 + 时间线条目，含淘汰版本数/链数）；禁止静默丢弃 | P0 |
| REQ-004-34 | 链完整性不变量：任何时刻链内每条记录的 `parentDraftId` 为 `null` 或指向同链存在记录；trim/截断/淘汰后仍成立（构造性遍历测试） | P0 |
| REQ-004-35 | store 文件读取上界 ≤ 32 MiB，且满额（8 链 × 16 版）合成文件可读；上界派生不得随 cap 悲观膨胀 | P1 |

### 11.5 跨入口（§8）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-36 | 三入口共享同一 store 文件（`<LocalAppData>/VFXComposer/AI/recipe-drafts.json`）；schema 升版为跨入口破坏性变更，要求 Desktop/`vfxc`/`vfxc-mcp` 同版本部署，文档明示 | P0 |
| REQ-004-37 | 混版两方向均 fail-closed 且不改写对方文件：新入口读版本 1 → `UnsupportedVersion` + 处置指引；旧入口读版本 2 → 只能给出 `StorageFailed`（如实记录为要求同版本部署的理由） | P0 |
| REQ-004-38 | 禁止新旧双文件并存式规避（不得改用新文件名让旧入口继续读旧文件），避免静默分叉 | P0 |
| REQ-004-39 | CLI/MCP 取草稿路径按新 schema 回归：`ListConfirmedAwaitingBuild` 顺序语义不变、`Confirm`/`MarkBuilt`/`MarkBuildFailed` 哈希绑定与前置状态不变 | P0 |
| REQ-004-40 | CLI/MCP 不得因 `origin` 值拒绝构建已确认版本；不新增命令/工具面（REQ-002 的 7 命令 + 8 工具闭集不变） | P0 |

### 11.6 参数面板与覆盖守卫（§9）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-41 | 面板按快照的参数键集、`type`、`[min, max]`、`default` 渲染并显示区间；草稿中未声明的键呈现为 L1.5 预警，不给编辑控件 | P0 |
| REQ-004-42 | 面板不能增删 stage/模块，不能改 `id`/`kind`/`templateId`/`attachTo`；`integer` 参数只接受整数，`float` 拒绝 NaN/Infinity | P0 |
| REQ-004-43 | 手改提交必须过 L1.5 + L1；越界/类型不符**拒绝提交**（不夹取、不静默纠正），提示精确到参数路径并给出允许区间 | P0 |
| REQ-004-44 | 手改通过校验后落新版本 `origin = human_edit`、状态 `PendingConfirmation`；全过程零 AI 请求、零网络、不消耗请求预算 | P0 |
| REQ-004-45 | 覆盖守卫在 AI 精修结果通过 L1 之后、落版本之前逐字段 diff；守卫域为模块参数标量，结构性差异不在守卫域；守卫不额外产生版本 | P0 |
| REQ-004-46 | 还原条件三条同时成立才还原（祖先链上存在未被覆盖的 `human_edit` 设定值 + 本轮反馈未点名 + AI 新值 ≠ 人改值） | P0 |
| REQ-004-47 | 点名判定确定性且保守：只用别名词表做 ordinal-ignore-case 词元/子串匹配，不做语义推断、不调用 AI、不用相似度启发式；未命中即视为未点名（→ 还原） | P0 |
| REQ-004-48 | 还原清单随版本持久化并两处可见（确认面板 + 时间线）；禁止静默还原 | P0 |
| REQ-004-49 | 守卫无用户开关；用户采纳 AI 值的唯一出口是在面板改成该值（产生新 `human_edit` 版本） | P1 |
| REQ-004-50 | 守卫确定性：同一（parent 链、AI 输出、反馈文本、词表版本）四元组产出同一还原清单（快照测试） | P1 |

### 11.7 艺术家知识（§10）

| 编号 | 需求 | 优先级 |
|---|---|---|
| REQ-004-51 | 片段含三段内容：反馈翻译表（含别名词表）、目录内美学惯例、精修纪律；内容要求见 §10.1 | P0 |
| REQ-004-52 | 翻译表覆盖率：目录中每个可编辑参数（当前 11 个）至少一条词条与一条别名；构造性遍历快照参数集断言，缺口即失败 | P0 |
| REQ-004-53 | 片段纯英文，不进 Desktop 双语 catalog，不含中文与 UI 文案键（扫描测试） | P0 |
| REQ-004-54 | 唯一真源为 `docs/ai-workflow/refine-artist-knowledge.md`；入库资产为 embedded resource `refine-artist-knowledge.fragment.json`，携带 `schema`/`version`/`source`/`exportedOn`（仿 catalog snapshot 纪律）；源与资产不同步即测试失败 | P0 |
| REQ-004-55 | 片段 `version` 递增反映到 `PromptTemplateVersion` 复合版本串（不触发 store 升版），并更新 prompt 组装快照测试 | P0 |
| REQ-004-56 | 别名词表与守卫点名判定消费同一份资产（禁两处漂移）；翻译表中的动作一律落在 `[min, max]` 内，越界动作写为"到界即止" | P1 |
| REQ-004-57 | 片段进入 prompt 后仍满足单消息 ≤ 16 KiB 与整请求 ≤ 256 KiB（有上界测试） | P0 |

---

## 12. 失败与边界行为汇总

| 情形 | 行为 |
|---|---|
| 空反馈 / 无 head 草稿即点精修 | 拒绝发起，零网络，输入态提示 |
| 反馈超 16 KiB | 拒绝发起，稳定码，不截断 |
| 组装后请求超 256 KiB | fail-closed 稳定码，不截断续传，不拆成多次请求偷预算 |
| ChatLlm 未绑定 / profile 禁用 | 网络前 fail-closed（继承 REQ-001 X1），指向 Settings |
| 精修网络失败 / 超时 | 恰 1 次请求，本轮终止，无版本产生（REQ-004-18） |
| 精修输出连续 L1 失败 | 用尽 `1 + N` 后本轮失败，head 不变，无新版本，保留最后输出与报告 |
| 精修输出结构合法但违反 strict 红线 | L1.5 预警呈现（v1 不阻断确认）；落版本照常。构建期由 L2/strict 审计权威拒绝 |
| 手改越界 | 拒绝提交，精确参数路径 + 允许区间，零版本、零网络 |
| AI 覆盖了未点名的人改参数 | 守卫自动还原为人改值，还原清单可见（REQ-004-45/46/48） |
| AI 覆盖了**已点名**的人改参数 | 放行 AI 值，不还原（这是用户本轮的意图） |
| 回退截断范围含已确认/已构建版本 | 拒绝截断，稳定码，提示另起新链 |
| 落新版本时同链有已确认未构建版本 | 该版本迁移 `Superseded`，退出待构建 backlog |
| 一条链达 16 版或 1 MiB | 从最老的非受保护版本开始 trim，提示可见 |
| 一条链的受保护记录已占满 cap | 拒绝新版本，稳定码，提示另起新链或清理 |
| lineage 数达 8 | 淘汰最久未活动的**整条链**，提示可见 |
| store 文件为版本 1 | `UnsupportedVersion` fail-closed + 删除重建指引；不自动删除/迁移；在用户处置前草稿功能不可用 |
| store primary 损坏、backup 可读 | 按 backup 恢复（现行 `LoadCore` 行为不变） |
| store primary 与 backup 均不可解析 | `StorageFailed` fail-closed，不清空重建 |
| 旧入口读到版本 2 文件 | 旧代码只能给出 `StorageFailed`；这是要求三入口同版本部署的直接后果 |
| `ui-preferences` 为 `/1` | 兼容读取，语言保留，模式取默认，下次保存重建为 `/2` |
| `ui-preferences` 为 `/2` 但含未知模式值 | 整文档不可用，回退默认 + `UI_PREFERENCES_UNUSABLE` |
| `ui-preferences` 存储不可用 | 会话内保持默认/内存态，记 `UI_PREFERENCES_STORAGE_UNAVAILABLE`（现行行为不变） |

---

## 13. 验收标准（Given / When / Then）

> AI 一律 mock/loopback；"零网络"指断言无任何 HTTP 活动。

**AC-1 示例卡零 AI 出草稿**
Given 简单模式、ChatLlm **未绑定**；When 用户点击任一示例卡；Then 产出一个新 lineage 的首版草稿（`origin = preset`、`revisionOrdinal = 1`、`parentDraftId = null`、状态 `PendingConfirmation`），零网络请求、零请求预算消耗，草稿通过 L1 + L1.5。

**AC-2 一轮精修成功**
Given 专业模式、head 为 `ai_draft` 版本、mock 一次返回合法修订；When 用户输入反馈并点击精修；Then 恰 1 次请求；落新版本 `origin = ai_refine`、`parentDraftId` 指向原 head、`revisionOrdinal + 1`、head 前移；时间线记录 1 次请求与 0 次重试；日志无 prompt 原文与反馈原文。

**AC-3 精修预算耗尽**
Given mock 恒定返回非 JSON 文本、N 配置为 2；When 用户点击精修；Then 恰 3 次请求（1 + 2）后本轮失败；head 与版本数**不变**；最后一版原始输出与错误报告可查看；无额外网络请求。

**AC-4 精修网络失败不自动重试**
Given mock 超时；When 用户点击精修；Then 恰 1 次请求、`TimedOut` 稳定码、无新版本；用户显式重发形成新一轮。

**AC-5 回退后再改不产生分支**
Given 链为 v1→v2→v3（head=v3），三者均 `PendingConfirmation`；When 用户回退到 v2 并确认丢弃提示，随后手改一个参数；Then v3 被删除；新版本 v4 的 `parentDraftId = v2`、`revisionOrdinal = 4`（序号不回收）；链内不存在两个 parent 相同的版本；`parentDraftId` 全链可解析。

**AC-6 截断被已确认版本拒绝**
Given 链为 v1→v2→v3 且 v3 状态为 `ConfirmedAwaitingBuild`；When 用户尝试回退到 v1；Then 截断被拒绝并给出稳定码，链与状态零改动。

**AC-7 手改越界被拒**
Given head 含 `PFT_2D_FireCore.scale`（区间 `[0.6, 2.4]`）；When 用户输入 `3.0` 并提交；Then 提交被拒绝，提示精确到该参数路径并给出 `[0.6, 2.4]`；**不夹取为 2.4**；无新版本、零网络。

**AC-8 手改落新版本并使旧确认失效**
Given v2 状态为 `ConfirmedAwaitingBuild`；When 用户在面板把一个参数改成区间内的合法值并提交；Then 落 v3（`origin = human_edit`、`PendingConfirmation`）；v2 迁移为 `Superseded` 且不再出现在 `ListConfirmedAwaitingBuild()`；v2 的 `recipeJson`/`canonicalSha256` 逐字节不变。

**AC-9 覆盖守卫还原未点名的人改**
Given v2 是 `human_edit`（用户把 `PFT_2D_FireTrail.width` 从 0.42 调到 0.20）；mock 精修返回把 `width` 写回 0.42、同时把 `PFT_2D_FireCore.scale` 从 1.2 改为 1.8；用户本轮反馈为"make the fire core bigger"（未点名 trail/width）；When 精修完成；Then 新版本的 `width` 被还原为 0.20、`scale` 保留 1.8；`guardRestorations` 含且仅含 `width` 一项；确认面板与时间线均可见该还原；守卫**未**额外产生版本（恰落 1 个 `ai_refine` 版本）。

**AC-10 覆盖守卫放行已点名的改动**
同 AC-9，但反馈为"shorten the trail and make it thinner"（命中 trail/width 别名）；Then `width` 保留 AI 值 0.42；`guardRestorations` 为空。

**AC-11 `/1` 偏好升级不丢语言**
Given `ui-preferences.json` 为 `/1` 且 `language = ChineseSimplified`；When 启动 Desktop；Then 界面为中文、模式为简单模式、无 `UI_PREFERENCES_UNUSABLE` 诊断；When 用户切换到专业模式；Then 文件被重建为 `/2`、`language` 仍为 `ChineseSimplified`、`generationMode = "Professional"`。

**AC-12 `/2` 未知模式值 fail-safe**
Given `/2` 文档的 `generationMode` 为 `"Wizard"`；When 启动；Then 语言与模式均回退默认、记 `UI_PREFERENCES_UNUSABLE`、程序正常启动；文件在下次显式保存前不被改写。

**AC-13 跨入口旧 store fail-closed**
Given 磁盘上的 `recipe-drafts.json` 为 `formatVersion = 1`（含若干旧草稿）；When 新版本 Desktop / `vfxc` / `vfxc-mcp` 任一读取草稿；Then 以 `UnsupportedVersion` fail-closed（**不是** `StorageFailed`）；给出删除重建指引且指引不含绝对路径；旧文件与 `.bak` 逐字节不变（未被删除、改名或迁移）；无静默重建的新文件出现。

**AC-14 lineage 级 trim 可见且保持链完整性**
Given 一条链已有 16 个版本，其中最老 3 个为非受保护的中间版本；When 落第 17 个版本；Then 最老的非受保护版本被 trim；用户看到一次性提示且时间线有条目；链内 `parentDraftId` 全部可解析（被 trim 版本的子版本 parent 已按实现定义的规则重挂或该子版本本身在被 trim 集合内——两种实现都必须保持 §7.1 第 4 条不变量，测试构造性遍历断言）。

**AC-15 全局级 trim 淘汰整条链**
Given 已有 8 条 lineage；When 用户点示例卡创建第 9 条；Then 最久未活动的**整条链**被淘汰（其全部版本消失，不留任何孤儿记录）；提示与时间线条目可见；其余 8 条链完好。

**AC-16 受保护记录占满 cap 时拒绝新版本**
Given 一条链的 16 个版本全部为受保护状态（head + 已确认/已构建）；When 用户尝试精修或手改；Then 新版本创建被拒绝并给出稳定码，提示另起新链或清理；既有记录零改动；若动作为精修则**不发起网络请求**（预检在请求前）。

**AC-17 CLI 构建任意 origin 的已确认版本**
Given `human_edit` 版本与 `preset` 版本各一个已确认（`ConfirmedAwaitingBuild`）；When 经 CLI 构建路径取草稿；Then 两者均出现在 backlog 且按确认顺序（最早在前）；`Superseded` 与 `PendingConfirmation` 版本不在 backlog；构建路径不因 `origin` 值拒绝。

**AC-18 精修为单段单 route**
Given 任意一轮精修；When 观察请求序列；Then 至多 `1 + N` 次请求且全部落在同一已解析 route（profile/model/endpoint/协议逐项相等）；不存在"规格调用 + recipe 调用"的两段结构；请求消息中不含上一轮反馈文本。

**AC-19 模式切换零副作用**
Given 存在 3 条 lineage 与若干版本；When 用户在两模式间来回切换 5 次；Then 零网络请求；全部草稿记录（含 `updatedUtc`）逐字节不变；偏好文件持久化往返一致。

**AC-20 时间线与 redaction**
Given 一轮包含 1 次修复重试与 2 项守卫还原的精修；When 查看任务时间线；Then 可见请求数 2、错误码序列、L1.5 issue 码、守卫还原计数 2 与两条参数路径、结果版本 id 与 `origin = ai_refine`、`PromptTemplateVersion` 复合版本串；时间线与日志中不出现 prompt 原文、AI 原始响应、反馈原文、endpoint、secret。

---

## 14. 与现有代码/schema 的映射

| 本文概念 | 现有代码/资产 | 关系 |
|---|---|---|
| 目录能力事实（6 模板 / 11 参数 / projectile / 2d / catalog 1.0.0 / 契约 1.4） | `src/VFXComposer.AI.Providers/Recipes/Assets/recipe-v1-template-catalog.snapshot.json` | 直接派生；REQ-004-04 禁止硬编码 |
| 参数上下界读取（面板与 L1.5） | `RecipeTemplateCatalogSnapshot.TemplateParameterSnapshot`（`MinLiteral`/`DefaultLiteral`/`MaxLiteral`/`Type`）、`RenderPromptTable`（当前唯一消费者） | 已有数据；新增 UI/校验消费者（F8a1 提供读取 API） |
| L1 结构校验 | `RecipeL1Validator`（模块 `parameters` 仅 `ReadObject(required: true)`） | 已有；**不校验键集与上下界**（F-3），差额由 L1.5 补 |
| L1.5 预校验与 `issueCode → 建议键` | 不存在 | 缺口，F8a1 |
| 精修请求预算常量 | `RecipeChannelLimits.DefaultRetryLimit = 2`、`MaximumRetryLimit = 5`、`MaximumDescriptionUtf8Bytes = 16 KiB`、`MaximumDraftJsonCharacters = 128 KiB` | 直接复用，不新增常量语义 |
| 请求/响应有界 | `ChatChannelLimits`（`MaximumMessages = 64`、`MaximumRequestBytes = 256 KiB`、`MaximumResponseBytes = 1 MiB`、`MaximumStructuredOutputSchemaBytes = 32 KiB`）；`RecipePromptAssembler.MaximumMessageCharacters = 16 KiB`（F8b1 前为 `RecipePromptTemplate`）；越界稳定码 `PayloadTooLarge` | 直接继承 |
| 唯一绑定调用与零 fallback | `ChatChannelGateway`、`ChatRouteResolver`、`IRecipeGenerationChannel` | 直接继承（ADR-006） |
| prompt 组装（片段化、多消息、复合版本） | **F8b1 已合入（2026-09-02）**：`RecipePromptAssembler` 吸收原 `RecipePromptTemplate`（非并存）。复合版本串 `AiContractVersions.RecipePromptAssembler = "vfxcomposer.ai.recipe-prompt-assembler/1"` + 8 片段 `id/version`（写入 `PromptTemplateVersion`，≤256 字符有守卫；store 未升版）；`SystemPrompt` 由 5 个片段拼接、与重构前逐字节等价（哈希 pin 测试）；只在片段边界拆分、不跨 role，单片段 >16 KiB / 消息数 >64 / 总字节 >256 KiB 一律 `PayloadTooLarge` 不截断；修复话术 "Fix only the listed errors…" 不变。F8b4 的精修知识片段在 `FragmentRegistry` 登记 | 本文的精修消息在其之上组装 |
| prompt 参考样例与 strict 红线 | 快照 `canonicalExample`（当前为 `fireball_2d`，8 模块 + 双 `attachTo`，F-2） | 由 **F8-0** 修正；本文以"F8-0 交付后的 prompt 红线"为前提 |
| 草稿记录与状态机 | `RecipeDraftRecord`、`RecipeDraftStatus`（`PendingConfirmation`/`Failed`/`ConfirmedAwaitingBuild`/`Built`/`BuildFailed`） | 纯加法扩字段（§7.2）+ 新增终态 `Superseded`（§7.3） |
| 哈希绑定确认 | `RecipeDraftStore.Advance`（`CanonicalSha256` 精确判等 → `HashMismatch`）、`Confirm`/`MarkBuilt`/`MarkBuildFailed` | 语义不变；新增 supersede 迁移同样做哈希绑定 |
| store wire 版本 | `RecipeDraftCodec` 的 `formatVersion`（`AiContractVersions.RecipeDraftRecordFormatVersion = 1`，读取精确判等） | 升 2，未知版本 fail-closed（仿 F3c） |
| store 容量与文件上界 | `RecipeDraftStore.MaximumRetainedRecords = 32`（静默 `.Take(32)`）、`MaximumFileBytes ≈ 18 MiB` | 被两级 cap 取代；trim 由静默改为可见；读取上界纪律见 REQ-004-35 |
| store 稳定错误码 | `RecipeDraftStoreErrorCode`（`NotFound`/`InvalidStatus`/`HashMismatch`/`StorageFailed`/`RecordInvalid`） | 新增 `UnsupportedVersion` |
| 原子写 + `.bak` 与损坏恢复 | `RecipeDraftStore.Persist`/`LoadCore`（`AtomicFileWriter.WriteReplace`、primary→backup 回落、双损坏 fail-closed） | 行为不变 |
| 三入口共享 store | `AiDesktopRuntimeFactory.CreateCurrentUser()`（`<LocalAppData>/VFXComposer/AI/recipe-drafts.json`）；`apps/VFXComposer.Cli/CliProductionEnvironment.cs` 的 `DesktopGenerationRuntime.DraftStore`；`src/VFXComposer.Batch.Core/RecipeGenerationJobExecutor`、`RecipeBuildOrchestrator` | 事实来源；§8 的同版本部署约束据此成立 |
| UI 偏好存储与解析纪律 | `UiPreferencesCodec`（`SchemaId = "vfxcomposer.ui-preferences/1"`、属性数硬判 `properties != 2`、语言名字面量常量）、`UiPreferencesStore`（`%LocalAppData%/VFXComposer/ui-preferences.json`、原子写、fail-safe 回退、三个诊断码） | 升 `/2` + `/1` 兼容读取（§5.3） |
| 双语文案机制 | F7a/F7b 的 `UiStringCatalog` + `LocalizationService`（184 键，平价与收尾断言） | 示例卡展示文案、建议句、守卫提示、trim 提示一律走 catalog 键 |
| 艺术家知识片段的入库纪律先例 | 快照的 `source` + `exportedOn` 头（"static export … re-export after a catalog change"）、`S12SlashAiExporter` 模式 | §10.2 仿此 |
| 预置骨架的 strict 合规先例 | `batches/recipes/spark_projectile_2d.json`（三 stage 根齐全、1 个渲染模块、无 `attachTo`，F6 真机验证过） | 示例卡骨架的形状参照 |
| 有界重试与零自动网络的授权 | `ADR-007 §2.5`（一次显式动作授权 `1 + N`、同一已解析 route、网络类失败不重试、预算进时间线） | 直接复用，精修不需要 ADR 改版 |
| 载荷可入 store 不可入日志的纪律 | REQ-003 §7.1、`CODING_STANDARDS` §3.1 | §6.3 直接继承 |

---

## 15. 缺口清单与任务卡对应

| 缺口 | 内容 | 归属任务卡 | 对应需求 |
|---|---|---|---|
| GG-1 | prompt 参考样例与 strict 红线修正（F-2） | **F8-0** | §6.1 前提、REQ-004-02 |
| GG-2 | L1.5 目录感知预校验 + `issueCode → 建议键` + 上下界读取 API（F-3） | **F8a1** | REQ-004-41/43，§5.2 第 4 条 |
| GG-3 | 简单模式界面：示例卡 + 预置骨架资产 + 能力提示 + 建议句 + 诚实提示 | **F8a2** | REQ-004-02~06 |
| GG-4 | PromptAssembler 重构（片段化、多消息拆分、复合版本串） | **F8b1** | REQ-004-19/55/57 |
| GG-5 | 草稿 store 版本链：schema 升 2、lineage/origin/parentDraftId、两级 cap、trim 可见、`UnsupportedVersion`、`Superseded` | **F8b2** | REQ-004-23~35、REQ-004-39 |
| GG-6 | 参数面板（渲染、手改预校验、落新版本） | **F8b3** | REQ-004-41~44 |
| GG-7 | 精修回路：三件套上下文、艺术家知识片段、覆盖守卫、版本链落盘、模式切换 UI（`ui-preferences /2`） | **F8b4** | REQ-004-01、08~22、45~50 |
| GG-8 | 艺术家知识源文档与入库片段 | **F8b4**（内容）/ **F8b1**（组装） | REQ-004-51~57 |
| GG-9 | Desktop 构建闭环（F-4） | **R6**（设计）/ **F8c**（实现） | 本文非目标 5 |
| GG-10 | `origin` 闭集扩为四值的追认 | 主 agent 裁决 | §7.2、O-4 |

---

## 16. 备选方案与否决理由

1. **旧 store 文件的处置**（对应 §7.4、§8）
   - *静默迁移版本 1 → 2（推断 `lineageId`/`origin`）*：否。版本 1 记录没有 lineage 概念，推断出的"每条记录一条单版本链、`origin = ai_draft`"是**编造的审计事实**；F3c 已定 fail-closed 先例，两个 store 用两套纪律会让"未知版本"这条规则失去可预期性。
   - *新版本改用新文件名（`recipe-drafts.v2.json`），旧文件原地不动*：否。旧入口继续读旧文件、新入口读新文件 → 两套入口各看到一半草稿的**静默分叉**，比停摆危险得多；且它会掩盖"必须同版本部署"这个真实约束。
   - *自动删除旧文件重建*：否。草稿是用户产物，程序不得为了自己好过而删用户数据。
2. **容量 cap 的形态**（对应 §7.5）
   - *沿用单级 32 条记录上限*：否。一条重度精修链即吃满全局配额，其他链被静默清空（REQ-001 O-2 正是为此而开）。
   - *跨链混合按 `UpdatedUtc` 淘汰单条记录*：否。会产生 `parentDraftId` 指向已被 trim 记录的孤儿版本，破坏链完整性不变量。
   - *无上限 + 用户手动清理*：否。store 是单文件全量读写，无界文件会把读取上界与启动耗时都变成不可控项。
3. **精修轮数硬顶**（对应 §6.2 第 6 条）
   - *给"每条链最多 N 轮精修"设独立硬顶*：否。它与版本链容量上限是同一件事的两种度量，两处配置必然漂移；由 cap 间接封顶只需维护一处数值，且用户看到的失败原因（"这条链版本已满"）与他能采取的动作（另起新链/清理）直接对应。
4. **`/2` 偏好文档的容错粒度**（对应 §5.3 第 4 条）
   - *逐字段容错（模式值非法只回退模式、保留语言）*：否（v1）。F7a 定版的解析纪律是"属性数硬判 + 精确判等 + 未知即不可用"，逐字段容错会开一个"部分接受畸形文档"的口子；`/2` 文档只由本程序写出，非法值只可能来自手工编辑或损坏，此时整体回退 + 诊断码是更可预期的行为。`/1` 的兼容读取是**明确列举的单一例外**（裁决要求，避免语言静默重置），不是通用容错策略。
5. **覆盖守卫的点名判定**（对应 §9.3 第 4 条）
   - *让 AI 自己声明"本轮改了哪些字段"*：否。要么改 schema 加声明字段（等于重开 IR 的小型版本），要么信任自由文本声明（不可测试、不确定）。
   - *用相似度/嵌入判定点名*：否。引入不确定性与额外调用，且无法写出稳定的快照测试。
   - *不做守卫，只把 diff 展示给用户让他自己改回去*：否。这把系统性问题转嫁为每轮的手工劳动，正是本次改版要消除的体验缺陷。
6. **`origin` 闭集**（对应 §7.2）
   - *沿用三值、示例卡记 `human_edit` + `presetId`*：可行退路，但审计时"用户手改参数"与"点了示例卡"混为一类，版本链视图也无法诚实标注来源。本文提案扩为四值并请主 agent 追认。

---

## 17. 开放问题与风险

### 17.1 风险

- **RG-1 表达空间是根本约束**：6 模板 / 11 参数下，精修能改的只有 11 个标量与有限的模块取舍。用户可能很快撞到"再怎么说也就这样"的天花板。本次改版能改善的是**可控性与确定性**，不是表达力；表达力的杠杆是模板库扩充（主计划暂缓项）。文档必须诚实呈现这一点（REQ-004-04/06）。
- **RG-2 L1.5 只是预警**：F8a1 定版 v1 不进重试预算、不改生成判定。于是"AI 出的草稿带 L1.5 预警但仍可确认"是常态；真正的拒绝发生在构建期。若实践中这条缝隙造成大量构建失败，需要重开"L1.5 是否进重试预算"的裁决（会牵动 ADR-007 §2.5 的预算条款措辞）。
- **RG-3 fail-closed 的一次性手工动作**：草稿 store 升版后，开发机与早期用户必须手动删除旧文件（F3c 已有同类先例：删除 `%LocalAppData%\VFXComposer\Jobs` 重建）。这是刻意代价，但必须在发布说明与 UI 提示里明确，否则会被当成崩溃。
- **RG-4 守卫的保守匹配会误还原**：点名判定取窄匹配，用户用了词表外的说法（"让尾巴收一点"而词表只有 "trail/streak/tail" 的英文别名）时，AI 的正确改动会被还原。缓解：还原可见 + 一键在面板采纳 AI 值（§9.3 第 6 条）；中文反馈的别名覆盖是 F8b4 的具体工作量（词表须含中文说法，尽管片段其余部分为英文——见 O-3）。
- **RG-5 两层预算不一致（既有债务）**：`BudgetCalculator`（mobile_medium，MaxMaterials=8）与 strict 审计（≤2 渲染模块）两层不一致且后者在构建后才跑（主计划债务项）。参数面板与 L1.5 都以 strict 为准呈现，用户看到的"合法"与 `BudgetCalculator` 的宽松结论可能不一致。本文不解决该债务，仅记录。
- **RG-6 单文件全量读写的并发面**：三入口共享一个 store 文件，现有实现是进程内 `lock` + 原子替换，**没有跨进程锁**。Desktop 精修与 CLI 构建同时写同一文件时，后写者会覆盖前者的记录集（last-write-wins）。版本链把单文件的记录数从 32 提到 128，写冲突窗口相应变宽。是否需要为草稿 store 引入 durable lock（仿 `ProviderConfigurationRevisionLock`）是 F8b2 的设计题，见 O-5。**已关闭（F8b2，2026-09-02）——冲突行为定义**：Desktop、`vfxc`、`vfxc-mcp` 共享同一草稿 store 文件；`RecipeDraftStore` 的每个公开成员（含只读成员）都把完整的 load→mutate→persist 周期包在一把 durable 跨进程锁内（独立锁类 `RecipeDraftStoreLock`）：锁锚文件 `recipe-drafts.json.lock` 与 store 同目录、永不删除，以独占打开句柄作为租约；同进程内的调用先经同一路径的 Monitor 串行。并发写者因此串行化——后到者在租约内读到先到者刚写入的记录并在其上追加，last-write-wins 丢记录不可能发生。等待有界（默认 5 s），超时即 fail-closed 抛 `RecipeDraftStoreErrorCode.StoreBusy`，此时不读、不写、不轮换 `.bak`、不产生临时文件，由调用方决定是否重试。持锁者被杀时 OS 释放句柄，文件本体因原子替换仍完整。文件不存在时的只读查询不取锁直接返回空。

### 17.2 开放问题

- **O-1** 示例卡骨架资产的落点：随程序入库为 embedded resource（与快照同域）还是放 `batches/recipes/` 复用现有样例目录？前者与 Desktop 无项目 I/O 的边界更契合，后者复用 F6 已验证的样例。建议前者，F8a2 定。
- **O-2** L1.5 预警是否阻断"确认"动作（不是阻断生成）：v1 按 F8a1 定版为纯预警，但"带已知 strict 违规的草稿可以被确认并提交构建"值得再看一眼。建议 v1 保持不阻断 + 确认面板显著警示，F8b3 交付后按实测失败率复议。
- **O-3（已裁决，2026-08-31）** 别名词表的语言：**批准词表条目携带中文别名字段**。裁决澄清"prompt 纯英文"（F7 裁决 2）的适用范围 = 送往 AI 的模板/片段文本；别名词表是覆盖守卫的**本地确定性匹配数据**，永不进入 prompt，允许双语。（用户反馈原文本就允许任意语言进入 prompt 的 user 内容位，与模板语言裁决无关。）
- **O-4（已裁决，2026-08-31）** `origin` 闭集扩为四值（新增 `preset`）——主 agent 追认批准，见 §7.2 与 §16 第 6 条。
- **O-5（已裁决，2026-09-02）** 草稿 store 跨进程冲突（RG-6）：**采用完整方案 durable lock**（主 agent 派发 F8b2 时定版，F8b2 交付 `RecipeDraftStoreLock` + `StoreBusy` 稳定码 + 他进程持锁/并发写者串行测试）。原最小方案"写前重读 + 按 `draftId` 合并"否决：与两级 cap/trim 重挂父链接叠加后合并语义不可判定。冲突行为定义见 RG-6。
- **O-6** 版本链信息是否需要在 CLI 可见（`vfxc` 列出 lineage/版本）。本文按非目标 3 排除；若批量用户需要按 lineage 复查，另立需求。

---

## 18. 变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.1 | 2026-08-31 | 初版（任务卡 R5 交付）：双模式定义、精修语义（锚定三件套 + 每轮 1+N + 无 IR 拆解段）、版本链（线性/回退截断/origin/Superseded/store 升版 2/两级 cap 16 版·1 MiB·8 链）、跨入口同版本部署约束、参数面板与精修覆盖守卫、艺术家知识章程、57 条编号需求 + 20 条 AC；关闭 REQ-001 开放问题 O-2（§7.5） |
