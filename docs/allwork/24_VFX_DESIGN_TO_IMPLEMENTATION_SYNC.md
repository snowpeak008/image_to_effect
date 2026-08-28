# W24 设计—实现同步与商用视觉生产重构计划

> 版本：1.4（Standalone Desktop 架构修订；保留 1.3 生产与证据基线）
> 日期：2026-08-26
> 状态：**用户已授权，W24 Goal 进行中；Desktop Phase 1 有界门禁通过、Phase 2 安全基础切片进行中但 production connection NO-GO**
> 定位：解决"功能协议已经实现，但设计文字、真实运动和最终画面严重脱节"的系统性问题。
> 权限边界：用户已授权按 S0a → S6 顺序推进，并将 S6 最终产品改为独立 Desktop + Unity Worker + 可选安全 Broker；仍不授权未经门禁安装第三方包，不授权未经所有权核验重写或删除既有资产，所有用户视觉签署权保持不变。
> 规范性附件：`docs/vfx-reviews/W24_1_1_INDEPENDENT_REVIEW.md`（1.1 版独立评审）、`docs/vfx-reviews/W24_1_2_FINAL_CONFIRMATION_REVIEW.md`（1.2 版最终确认审查，结论 GO-WITH-EDITS）。本文与附件冲突时以本文为准；本文未展开的细节表（像素量测降级表、mutant 类别表、校准指标定义）以附件为规范来源。

---

## 0. 修订记录

### 0.0 1.4 Standalone Desktop STOP-THE-LINE

2026-08-26，用户决定立即停止 Unity Editor 主界面的新增 UI、五标签页、Player UI、美化和嵌入式 MCP 入口开发。既有 Unity UI、Models、Tests 与 r31/r32/r35/r36 收据保留为兼容/诊断基线；独立 Desktop 达到功能对等并通过门禁前不得删除，之后也只能经用户确认降为 hidden diagnostic fallback。

新的最终产品目标是：`.NET 8 + Avalonia + MVVM` 独立桌面主界面、纯 C# Shared Protocol、Unity Package Worker，以及可选的独立安全 Broker。Windows 第一阶段只允许 authenticated named pipe；Desktop 不直接写 Unity 工程；没有可信 Broker issuer 时 production real-read 继续 `W24FS001`。规范性决策见 `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md`。

Phase 0 已通过独立架构审计。Phase 1 的纯 C# Protocol、disconnected Client 和 Avalonia Desktop shell 已完成有界实现、机器收据和最终冻结字节审计；审计为 `P0=0 / P1=0 / P2=5`，结论严格限定为 `DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY`。Phase 2 现仅完成十类协议与 dormant Broker 安全基础，包括有序 grant/ACK/revoke/ACK 与精确进程终止收口；Unity Worker 另有严格 grant/revoke codec 和 test-only opaque 三句柄 admission/close owner，r48 为 admission 13/13、protocol 6/6。r6 以实际 Unity 2022.3 Editor 进程跑通 test-only 生命周期 2/2，read-query r4 以 test-issued lease 跑通四类 handle-relative 读取 14/14，r11 再把它们接成 non-publishable HandleProbe→实际 Unity 五次 test-pipe 查询并获 `P0=0 / P1=0 / P2=0` scoped GO。最新 .NET r5 新增无路径 Client 查询编排和第二条 authenticated Desktop 测试管线，136/136 通过；r2 因 stale publication/递归清理被拒，r3 又因锁序反转/child-first cleanup 被拒，r4 虽闭合锁序却仍保留路径删除 TOCTOU、production-compiled test barrier 与非聚合 fixture cleanup。r5 改为 pinned-handle/no-follow 测试清理、删除产品 test hook，并在尝试全部 fixture cleanup 后聚合失败；它以 `P0=0 / P1=0 / P2=0` 获得 `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY` scoped GO。其 adapter 仍只在 Broker 测试程序集。无测试宏的独立 Editor 编译仍不含 Worker test issuer/hooks；HandleProbe 不可发布。production policy/session/global process-ownership/ACK issuer、authenticated production Unity connector、production Client pipe connector、Desktop 结果展示和真实注册项目读取仍未实现，Broker policy 继续在 listener 前 `W24FS001`。Phase 1 五类 status DTO 仍只是非授权展示合同，disconnected smoke、.NET transport tests 与 Worker tests 都不是像素/视觉 QA。精确边界见 Phase 1、Phase 2 foundation、各 Worker/Broker 报告以及 `W24_PHASE2_DESKTOP_BROKER_READ_TRANSPORT_REPORT.md`。

### 0.1 1.3 授权记录

2026-08-25，用户确认终审遗留的两处小冲突修正后，明确授权启动 W24 全项目 Goal。授权含 §22 的实施决策与 S0a 开工；不构成任何历史视觉条目的自动 L4 签署，也不允许执行 Agent 代替用户完成 S0b/S3/S4 的视觉与迁移裁决。

同日用户进一步指定：**所有人工视觉验收集中到全量内容开发之后执行，开发过程不得因等待用户看图而中断。** 因此 S0b/S3/S4 的视觉签署从“后续源码开工阻塞门禁”调整为“最终完成阻塞门禁”：机器开发可以在条目标记 `VISUAL_PENDING`、不授予 L4 的前提下连续推进；W24 最终完成定义与用户 L4/迁移裁决权不变，未签署条目不得对外称为 production ready。

### 0.2 1.2 → 1.3 的差异（按最终确认审查 GO-WITH-EDITS 七组修订执行）

最终确认审查判定 1.2 主体架构成立、无 NO-GO 问题，但列出授权 S0a 前必须完成的七组文字修订，本版全部落实：

1. **证据权威路由（13.0 / 13.2b / 21 节）**：每条 requirement 显式声明 `evidenceAuthority`（telemetry / diagnostic / visualQa / user，S0a 另有 calibrationLabels）；把 1.2 中"所有 visual requirement 都以 Diagnostic Pass 为权威"的过宽表述拆为"可量测视觉事实 → diagnostic 权威"与"定性视觉语义 → visualQa 权威"；复合要求必须拆分 requirementId；13.2b 标题更正。
2. **S0a 双终态（17 / 4 节）**：定义 `S0A_GATE_QUALIFIED`（完整盲测达标，QA 获普通 L3 门禁权）与 `S0A_ADVISORY_ONLY`（QA 必跑不阻断，不能生成普通 L3，进 S0b 需用户显式授权）。
3. **缩减集口径与标签权威（17 节）**：缩减集定为 36 fail / 12 pass / 12 uncertain / 6 invalid，顺序检验后补齐 60/20/20/10；最终标签权威为冻结带 hash 的 `calibration-labels.json` 人工裁决清单，画面不可见的注入错误从视觉混淆矩阵剔除。
4. **指标分层（10.4 / 17 节）**：per-requirement 三态指标与顶层五路路由指标分别计数；`EVIDENCE_INVALID` 独立召回率；`CONTRACT_AMBIGUOUS` 首轮不设指标。
5. **用户入口双轨（14.2 / 5.1 / 4 节）**：普通签署入口要求 `VISUAL_PASS`；`VISUAL_UNCERTAIN`、advisory 模式、`CAPTURE_BLOCKED`、校准争议走带醒目标记的用户升级入口，消除"必须先 pass 才能见用户"的死锁。
6. **回路终态闭合（5.1 / 10.4 节）**：状态图枚举与 10.4 统一；补 `MACHINE_FAIL → C{n+1}`（机器门禁失败不进 QA）；`EVIDENCE_INVALID` 二次失败 → `CAPTURE_BLOCKED → NEEDS_USER_DECISION`；同一 effect 连续第二次重开合同需用户确认；`NEEDS_USER_DECISION` 明确由工作流聚合器发出。
7. **术语统一**：`revision` 统一为 `contractRevision`；"non-visual fail" 改为具体 requirement 类型；"0 漏检"限定为冻结的已知作弊测试集（frozen known-cheat corpus）。

1.2 版对评审的唯一保留意见（S0a 样本弹性）已按终审"有条件接受"方案定稿，不再是开放分歧。

### 0.3 1.1 → 1.2 的差异（保留存档；按独立评审 7.3 节五项修订执行）

独立评审结论为"有条件通过（Conditional GO）"，本版全部采纳其五项修订，另采纳其对回路、L4 绑定、批量初筛等条款的修正：

1. **S0 最小合同扩充（原 7.9）**：从四段扩为十段，补 Capture Profile、参考角色与权重、空间尺寸、seed、清理语义、最小预算、requirement 分类（visual / behavioral / structural / budget）和证据定位。见 7.9。
2. **像素门禁重构（原 13.2b）**：放弃"从普通 RGB 截图反推所有内部语义"。改为**四路证据包**：Beauty 连续帧（供人和视觉 QA 看）、Diagnostic Render Passes（Object-ID / Mask / Depth / 线性亮度，供机器量测）、Semantic Telemetry（组件与状态读回，作为内部事实的权威证据）、Human Verdict Corpus（用户签署/拒绝黄金案例库）。每类要求明确**一个权威证据 + 至少一个独立交叉证据**，不再机械要求"至少两层拒绝"。见 13 节。
3. **S0 拆分**：拆为 S0a（受控错误样本 + 视觉 QA 盲测校准）与 S0b（正式持续火焰垂直切片），避免用同一案例既校准 QA 又证明 QA 有效。见 17 节。
4. **Capture 元数据强化**：截帧证据必须携带 source hashes、Capture Profile 和 diagnostic pass manifest；采集环境明确为**带图形设备的 batchmode（禁止 `-nographics`）**。见 11.2.7。
5. **完成定义修正（原 21 节）**：取消"100% Required 要求都有截帧判定"，改为 visual requirement 由截帧+诊断 Pass 判定、非 visual requirement 由遥测/构建证据判定。见 21 节。

同步修正：回路规则改为 C0/C1/C2 不可变候选包 + 合同冻结与 `contractRevision` 边界 + uncertain 三分类（5.1 节）；L4 签署绑定 `contractRevision + buildHash + captureProfile`，重建即过期回 L3（4 节）；既有资产批量初筛只输出风险分与抽样建议、不直接定级（15 节）；参考图必须标注参考角色与权重（7.9 / 14 节）；MCP 评估明确排到 S1 之后（16 节）。

**本版对评审的唯一保留意见（待 Codex 确认）**：S0a 校准集规模（60 fail / 20 pass / 20 uncertain / 10 invalid）以"参数化 Patch 脚本批量生成"为前提；若脚本化生成不可行、手工成本超限，允许首轮缩减为每类 3 个（约 36 个 fail）起步并如实声明统计置信度的下降，后续批次补齐。见 17 S0a。

### 0.4 1.0 → 1.1 的差异（保留存档）

1.0 版的核心缺陷：整个体系没有任何环节让 AI 真正"看"渲染出来的画面。1.1 版三处结构性修订：① 视觉 QA 升级为读截帧的视觉 Agent，与实现者形成迭代回路；② 增加渲染帧量测门禁（1.2 已按评审重构为四路证据）；③ 实施顺序改为垂直切片优先。次要修订：明确 Skill 与 Agent 分工；模型附着基线默认用 Unity 官方示例模型；删除 OpenAI Skills API 引用。

---

## 1. 为什么需要这个工作包

现有项目已经证明可以建立 Recipe、模板、编译器、运行时协议、Patch、预览场景和机器测试，但多轮人工验收暴露出一个更基础的问题：

> **机器验证了"组件在动"，却没有验证"它是否按照设计含义在动"。**

典型问题包括：

- "碎片旋转"被实现成一整张网格或整组图片共同旋转，而不是碎片独立运动。
- "多阶段技能"被实现成几张互不相关的静态图按时间开关，没有蓄力、释放、命中、残留之间的因果连续性。
- "3D 特效"仍主要依赖面片和二维图片，只是改变观察角度，并没有真正依附模型、骨骼、空间体积或场景受光。
- "持续燃烧""移动弹丸拖尾""模型附着""真实光照"等常见能力没有形成可复用的正式制作底座。
- 当前测试主要统计可见像素、对象数量、时间窗口和是否出界，无法判断视觉语义、运动逻辑、层级关系和商用品质。
- 一个通用载体被过度复用到许多类型，导致 Recipe 名称变了，实际表现方式没有改变。

根因分析：

- **静默替换语义**：实现者为了方便改变设计含义 → 设计合同 + 禁止替代。
- **验证维度错位**：机器测的是"存在"，人看的是"含义" → 语义门禁 + 四路证据交叉验证。
- **AI 没有视觉反馈**：AI 从头到尾没看过自己产出的画面 → 视觉 QA Agent + Beauty 帧回路。

因此，现阶段不能继续依靠"批量生成更多类型"来解决质量问题。必须先建立一条能够约束设计、实现和验收、且包含视觉反馈回路的生产链。

**目标定位（按评审修正）**：本工作包的正确目标是"**设计可执行、实现可追踪、明显错误不过门、用户决定最终品质**"。它不承诺、也无法承诺自动产生商用级审美——Design Skill、视觉 QA 和像素门禁全部完成后，能做到的是：设计意图被显式记录、语义偷换更容易被发现、明显错误更早被拦截、用户拒绝意见积累为项目经验。

---

## 2. 总目标

建立项目自己的 **Unity VFX Design Director Skill + 设计合同 + 技术实现 + 视觉审查 Agent + 四路证据包** 体系，使设计文字中的每项关键语义都能追踪到真实 Unity 组件、运行状态、渲染画面和验收证据。

完成后，生产流程应当满足：

1. 设计不是一段模糊描述，而是一份机器可读、人工可审的设计合同。
2. 技术实现者只能在合同允许的范围内选择实现方式，不能偷偷改变设计含义。
3. 不同视觉含义必须使用适合的载体，禁止用单一图片、单一面片或单一旋转动画冒充所有效果。
4. 每类验收要求有明确的**权威证据**（内部事实以组件遥测为权威，画面事实以诊断 Pass 量测为权威）和至少一个独立交叉证据；视觉 QA Agent 在用户之前对照合同看 Beauty 帧、拦截明显视觉缺陷；人眼验收负责构图、层级、质感、可读性和商用判断。
5. Agent、自动测试和文档作者都无权替代用户签署最终视觉通过。
6. 在四条代表性生产链真正通过前，不再批量宣称"支持某类商用特效"。
7. 用户亲自验收之前，每个候选条目必须已经通过视觉 QA Agent 的截帧审查；用户不应该再是第一双看到画面的眼睛。

---

## 3. 非目标

本工作包不承诺以下内容：

- 不承诺"一句话自动生成任意商用级特效"。
- 不承诺视觉 QA Agent 能替代用户的审美判断——它拦截"明显不对"，不判定"足够好"；对"高级感、风格统一、商业竞争力"的判断不可靠，这部分归用户。
- 不承诺视觉 QA 是未知错误的可靠检测器——它是开放式补充防线，仍受提示词、参考、模型偏差和输入分辨率约束；其能力边界以 S0a 盲测结果为准。
- 不再扩展 Unity Editor 内的 VFX Studio 主界面；独立 Desktop UI 按 1.4 Phase 0–5 计划实施。
- 不在本阶段开发 Unity 内嵌 AI 聊天窗口。
- 不在本阶段扩展 Cocos 或 Unreal Engine。
- 不直接把整个项目迁移到 VFX Graph。
- 不把网上找到的 Skill、MCP 或示例项目未经验证直接接入主工程。
- 不把机器测试通过等同于视觉通过。
- 不因为建立了新计划，就自动将既有资产删除、降级或重建。

---

## 4. 统一的真实状态等级

后续每个 Runtime Entry 必须标注以下等级之一，不再只写笼统的"完成"：

| 等级 | 名称 | 含义 |
|---|---|---|
| L0 | Invalid / Missing | 无法构建、缺依赖、缺运行入口或行为错误 |
| L1 | Functional Protocol Complete | 协议和运行机制成立，但没有证明视觉设计成立 |
| L2 | Visual Placeholder | 可用于功能演示，但视觉载体、质感或语义仍是占位实现 |
| L3 | Production Candidate | 设计合同、连续运行、机器门禁和视觉 QA Agent 截帧审查均通过，等待用户签署 |
| L4 | User-Signed Production Ready | 用户在规定场景和动态流程中签署通过，可作为正式生产基线 |

规则：

- 未经用户签署的历史条目不得自动标为 L4。当前已完成源码和机器门禁的内容，依据真实状态重新评为 L1、L2 或 L3。
- 没有经过视觉 QA Agent 看过真实截帧的条目最多 L2。
- **（1.2 新增）L4 签署绑定具体 `contractRevision + buildHash + captureProfile`。任何改变视觉输出的重建、迁移或参数修改都使签署过期，条目回到 L3 重新走 QA 与签署，不存在"永久 L4"。**
- **（1.3 修正）视觉 QA Agent 的门禁权限取决于 S0a 终态（见 17 节）**：`S0A_GATE_QUALIFIED` 后 QA 判定才能作为普通 L3 门禁；`S0A_ADVISORY_ONLY` 状态下 QA 报告必跑但不阻断，此时**不能生成普通 L3**——条目要么停留 L2，要么由用户经 14.2 的带醒目标记升级入口显式 override 进入验收；advisory 报告不得写成 QA pass。

---

## 5. 新生产链

```text
用户目标 / 参考图（含角色与权重标注） / 游戏语境
          ↓
VFX Design Director Skill（设计 Skill）
          ↓
VfxDesignContract（设计合同，requirement 分四类）
          ↓
Schema + 语义 + 合法载体校验
          ↓
Technical VFX Implementer（主执行 Agent）
          ↓
Unity Runtime Entry
          ↓
候选包 C{n}（不可变：合同 hash + 构建 hash + 采集 hash + 证据 hash）
          ↓
四路证据采集（同一正式相机）：
  Beauty 连续帧 / Diagnostic Passes / Semantic Telemetry /（对照）Human Verdict Corpus
          ↓
机器门禁（结构 + 语义遥测权威 + 诊断 Pass 量测 + Beauty 交叉）
          ↓
Visual QA Agent（全新隔离会话，读 Beauty 帧，逐条对照合同）
          ↓                    ↑
          └── implementation fail：进入下一候选 C{n+1}（至多 C2）──┘
          ↓ 通过
用户最终动态验收与签署（结论写入 Human Verdict Corpus）
```

### 5.1 回路规则（1.2 按评审重写）

**候选包定义**："一轮"= 一个不可变候选包 C{n}，由合同 hash、Prefab/Manifest hash、Capture 工具 hash、证据 hash 和 QA 报告构成；证据目录 write-once。每个条目最多 3 个候选 **C0 / C1 / C2**，不是"初版后再附加 3 次修复"。

**合同冻结**：进入 C0 后合同冻结。任何对 required 要求、参考角色或禁止替代的改变必须升 `contractRevision`，回到 Design Director（必要时用户），不计入同一修复回路。

**QA 会话隔离**：每个候选使用全新隔离 QA 会话，先只看当前合同与当前证据，不读实现者自辩和上一轮解释；聚合器在审查完成后再比较历史差异，防止锚定。

**全量回归**：每个候选必须重审全部 required 项并产出 per-requirement regression diff，不得只复查上轮 fail 项。

**seed 规则**：至少 1 个 canonical seed + 2 个固定 robustness seed。用户可只看 canonical；机器与 QA 必须确认三个 seed 均无明显崩坏，防止挑 seed 作弊。

**uncertain 三分类**：QA 不确定结论先分流，只有真正的视觉歧义升级用户：

- `EVIDENCE_INVALID` → 退采集工具链重采（不计实现轮次，至多重采 1 次；第二次仍失败 → `CAPTURE_BLOCKED`，由工作流聚合器升 `NEEDS_USER_DECISION`）；
- `CONTRACT_AMBIGUOUS` → 退设计，升 `contractRevision`（同一 effect 连续第二次重开合同必须先经用户确认，防止借 `contractRevision` 绕开 C0/C1/C2 上限）；
- `VISUAL_UNCERTAIN` → 经 14.2 带醒目标记的用户升级入口交用户裁决。

**状态图**（1.3：枚举与 10.4 精确一致；`NEEDS_USER_DECISION` 与 `CAPTURE_BLOCKED` 由工作流聚合器发出，不是 Visual QA 的输出）：

```text
C{n} → 机器门禁
 ├─ MACHINE_FAIL → C{n+1}（不进入 QA）
 └─ 通过 → Visual QA
      ├─ VISUAL_PASS → 用户（普通签署入口）
      ├─ VISUAL_FAIL → C{n+1}
      ├─ EVIDENCE_INVALID → 重采集（不计轮次，至多 1 次；再失败 → CAPTURE_BLOCKED → NEEDS_USER_DECISION）
      ├─ CONTRACT_AMBIGUOUS → 回设计，新 contractRevision（连续第二次重开需用户确认）
      └─ VISUAL_UNCERTAIN → 用户（带标记升级入口）

C2 之后仍 VISUAL_FAIL / MACHINE_FAIL → NEEDS_USER_DECISION / redesign（聚合器发出）
```

**提前退出**：若 C0 暴露载体级或合同级根本冲突，不消耗 C1/C2，立即退回设计。

**防指标优化**：禁止只针对 fail 的量测数值做优化；每个候选的 Beauty 帧、诊断 Pass、组件遥测和整体视觉 QA 必须同时回归。

**用户拒绝的版本规则**：用户拒绝生成新 requirement 或新 cheat pattern，旧候选保留拒绝记录入 Human Verdict Corpus；不得覆盖旧证据后重新命名为 pass。

责任回退：设计意图不清或自相矛盾 → Design Director；载体、组件、动画或绑定错误 → Implementer；证据链不真实、关键帧缺失或测试过弱 → QA 工具链；视觉不符合用户判断 → 不得以机器测试或视觉 QA 通过为理由覆盖用户结论。

---

## 6. 三个执行角色与一个签署角色

### 6.0 角色的落地形态

| 角色 | 落地形态 | 理由 |
|---|---|---|
| VFX Design Director | **Skill**（确定性流程 + Schema + 模板 + 参考知识库） | 设计产出需要稳定、可复现、可版本化；它是项目规范与合同生成器，不冒充审美模型 |
| Technical VFX Implementer | **主执行 Agent 本身**（受合同约束） | 实现需要全工具权限和长上下文 |
| Independent Visual QA Reviewer | **独立视觉子 Agent**（具备图像输入能力，每候选全新隔离会话） | 必须真的"看"截帧；隔离才能避免自我合理化与历史锚定 |
| 用户 | 唯一签署者 | 不变 |

### 6.1 VFX Design Director

职责：

- 把自然语言、参考图和游戏用途转换为正式设计合同。
- 拆分视觉层、时间阶段、空间锚点、运动关系、材质层级和预算。
- 明确哪些内容必须真实存在，哪些允许替代，哪些严禁替代。
- 为每个关键设计要求生成可追踪的 `designRequirementId`，并标注 requirement 类型（visual / behavioral / structural / budget）。
- 为每个 **visual** requirement 写出"视觉判定描述"+ 证据定位（对应状态、帧区间、ROI 或层 Mask），供视觉 QA Agent 逐条执行。
- 为每张参考图标注**参考角色与权重**（只参考构图 / 色彩 / 材质 / 运动 / 气氛中的哪几项，及明确不要求复制的内容）。

禁止：

- 不直接修改 Unity 资产。
- 不以"实现方便"为理由改变设计语义。
- 不签署自己的设计已经实现成功。

### 6.2 Technical VFX Implementer

职责：

- 按合同选择 Particle System、Trail、Mesh、Shader、Light、Model Binding 等载体。
- 记录每个 `designRequirementId` 对应的 Unity 对象、组件和参数。
- 保持状态之间的运动和视觉因果连续。
- 产出预算、依赖、所有权和构建清单。
- 每轮实现后自行运行采集，先看一遍自己的 Beauty 帧再提交视觉 QA——不允许在从未看过画面的情况下宣称完成。

禁止：

- 不得自行把独立碎片改成整体旋转。
- 不得把持续火焰改成循环播放整张图片。
- 不得把真实光照改成只有 Additive 材质的假亮。
- 不得在合同未允许时使用"最接近的通用模板"代替缺失载体。
- 不得手工投喂截图、挑选 seed 或修改证据目录。

### 6.3 Independent Visual QA Reviewer

形态：独立的视觉子 Agent，输入为设计合同、参考图（含角色权重标注）、关键帧、连续帧条（filmstrip）、多视角帧和机器门禁报告；每候选全新隔离会话，不读实现者的自辩说明。

职责：

- 逐条对照合同 **visual** requirement 的视觉判定描述与证据定位，在 Beauty 帧上给出 pass / fail / uncertain 三态判定与偏差描述。
- 检查真实连续播放、关键帧与合同状态机的一致性。
- 按参考图**标注的角色与权重**检查偏离（只审被标注的维度，不默认要求像素级相似）。
- 主动寻找第 9 节错误替代目录中的作弊模式在画面上的表现。
- 校验证据元数据一致性（帧号、状态、seed、相机、hash），不一致输出 `EVIDENCE_INVALID`。
- 输出结构化审查报告（`VFX_VISUAL_REVIEW`），失败项必须附具体帧号和画面位置描述。

禁止：

- 不修改被审资产。
- 不替实现者解释不符合合同的结果。
- 不代替用户给出 L4 结论。
- 不审 behavioral / structural / budget requirement 的内部事实（那是遥测的权威范围），只在画面表现与遥测矛盾时上报冲突。
- 不在没有实际读入图像的情况下产出任何判定——纯文本推断的审查报告一律无效。

能力边界（如实声明，以 S0a 盲测为准）：可靠拦截"碎片是不是整图在转""火停了粒子还在飘""光有没有照亮地面""阶段切换是不是硬开关"这类用户一眼能看出的语义级缺陷；对顶级审美、微妙质感和商用竞争力判断不可靠。它的价值是把用户从第一轮筛查中解放出来。

### 6.4 用户

用户拥有唯一的最终视觉签署权。Agent 只能提供证据、差异和建议。用户的每次签署与拒绝（含原因、帧号、合同修订）写入 Human Verdict Corpus，形成项目自己的可检索黄金案例库。

---

## 7. `VfxDesignContract` 最小契约

合同字段集合分两步落地：**S0 阶段只使用 7.9 的最小合同**；完整 Schema 在切片验证后由真实经验反推定稿。7.1–7.8 作为完整版候选字段清单保留。

### 7.1 身份与用途

- `contractVersion`、`effectId`、`displayName`、`archetype`、`dimension`
- `lifecycle`：one-shot / loop / sustained / interruptible / stateful
- `gameplayPurpose`、`readabilityGoal`、`targetPlatform`、`qualityTier`

### 7.2 参考与来源

- `references[]`；每个参考的角色与权重（构图 / 材质 / 运动 / 颜色 / 气氛）
- 文件 SHA-256、来源、许可和是否可进入最终资产
- 明确"参考目标"与"可直接使用资产"的区别，以及明确不要求复制的内容

### 7.3 空间合同

- 原点和朝向、视觉锚点
- 世界空间 / 局部空间 / 屏幕空间
- 设计尺寸、游戏尺寸和预览尺寸
- 相机姿态、FOV、背景、HDR、MSAA、后处理
- 模型或骨骼插槽；发射点、命中点、法线、目标点和可选地面

### 7.4 语义状态机

- 状态名称和时长；进入条件和退出条件
- 中断、循环和完成出口
- 状态之间必须保留的对象、能量、方向或残留
- 触发的 gameplay / VFX 事件
- 同 seed 的确定性要求

### 7.5 视觉层

每一层必须独立声明：`layerId`、`responsibility`、`carrier`、`geometry`、`materialModel`、`colorRole`、`blendMode`、`motionModel`、`timing`、`attachment`、`continuityFrom` / `continuityTo`、`budgetCost`、`required` 或 `optional`。

### 7.6 允许与禁止替代

- `allowedSubstitutions[]`、`forbiddenSubstitutions[]`
- 如果所需载体当前不存在，结果必须失败或降为 L2，不能静默改设计。

### 7.7 预算

- 粒子峰值和稳态值；Renderer 数量；Material / Shader Variant 数量
- Trail 顶点和长度；Light 数量、类型、范围、阴影策略
- Texture 本地独占和完整依赖驻留两栏；Mesh 顶点和实例数
- CPU / GPU / Overdraw 报告口径

### 7.8 验收

- 机器可判定规则
- 每个 visual requirement 的视觉判定描述与证据定位
- 人工视觉规则
- 必须提供的关键帧与完整连续录制（完成帧、停止帧、循环稳定帧和中断帧）
- 设计要求到实现对象的追踪表

### 7.9 S0 最小合同（1.2 按评审扩为十段）

S0 阶段强制以下十段，其余字段可选：

1. **身份与版本**：`contractVersion`、`effectId`、`contractRevision`、合同 hash。
2. **生命周期**：`sustained`；start / steady / stop / interrupt 的入口、期限和完成条件。
3. **参考角色**：每张参考图的 hash、来源、参考角色与权重（构图 / 颜色 / 材质 / 运动中的哪几项）、明确不要求复制的内容。
4. **空间与尺寸**：原点、朝向、锚点、设计尺寸、游戏距离尺寸。
5. **Capture Profile**：Unity/URP 版本、相机（序列化引用）、分辨率、fps、背景、Color Space、HDR、MSAA、Bloom、Tone Mapping、seed（canonical + robustness）。
6. **语义状态机**（7.4 全部字段）+ 每个 transition 的 `continuityMode`：continuous / impulse / replace / clear。
7. **视觉层**：7.5 的 `layerId` / `responsibility` / `carrier` / `geometry` / `colorRole` / `blendMode` / `motionModel` / `timing` / `attachment` / `continuityFrom/To` / `required`。
8. **禁止替代**（7.6；S0 采用"默认不允许、逐条白名单"）+ **清理语义**：`cleanupDeadline`、`allowedResidualLayers`、Light 停止期限。
9. **最小预算**：粒子峰值/稳态、Renderer、Material、Light、Texture 驻留。完整 CPU/GPU Profile 为 report-only。
10. **验收与证据定位**：requirement 分类（visual-measurable / visual-semantic / behavioral / structural / budget）+ 每条 requirement 显式声明 `evidenceAuthority`（telemetry / diagnostic / visualQa / user，见 13.0）；每个视觉 requirement 的判定描述 + 状态 + 帧区间 + ROI 或层 Mask；关键帧清单。同时含内部事实与画面事实的复合要求（如"真实光照""停止清理"）必须拆成多个 requirementId，各自唯一权威。

S0 明确砍掉：完整目标平台矩阵（只固定 PC Editor / Windows Player）、Small/Standard/Hero 多质量档（只做一档）、Shader Variant / Overdraw / GPU 时间正式阈值（report-only）、与火焰无关的模型/骨骼/命中法线字段、完整通用 Schema 的所有 Archetype 分支。

---

## 8. 视觉载体矩阵

"特效类型"和"实现载体"必须分开。每个视觉层必须主动选择载体，不能默认退回通用面片。

| 载体 | 适合内容 | 不适合冒充的内容 |
|---|---|---|
| Sprite / Flipbook | 细节丰富、已烘焙的小型动画 | 需要真实空间体积、模型依附或动态光照的主体 |
| Particle System Billboard | 火花、烟、尘、碎屑、飞沫 | 需要确定轮廓的大型主体 |
| Particle System Mesh | 冰片、碎片、落叶、符文片 | 独立模型角色或复杂 Skinned Mesh |
| Particle Trails | 火花尾迹、弹丸余迹、能量丝 | 静止时仍应存在的实体结构 |
| TrailRenderer | 移动物体路径、武器挥砍轨迹 | 不依赖真实运动的静态装饰线 |
| LineRenderer | 激光、链路、边界、路径 | 大面积体积火焰 |
| Procedural Mesh | 环、弧、扇形、可控碎裂面 | 高细节写实材质的全部内容 |
| Imported Mesh | 武器、法器、碎块、真实几何轮廓 | 仅用来给二维图片增加"3D"标签 |
| SkinnedMeshRenderer | 角色表面溶解、能量流、骨骼附着 | 独立悬空面片 |
| Shader / Shader Graph | 扭曲、溶解、流动、边缘光、程序纹理 | 缺少实际对象和状态逻辑时的万能替代 |
| Light / URP Light2D | 对场景产生真实照明变化 | 只画一个亮色圆形 |
| Decal | 地面烧痕、命中残留、范围标记 | 悬空主体 |
| UI Graphic | 屏幕警告、HUD 特效、遮罩 | 世界空间战斗特效 |
| VFX Graph | 大规模 GPU 粒子、复杂模拟 | 当前 Unity 2022.3 URP 下未经兼容性验证的默认底座 |

---

## 9. 强制禁止的错误替代

以下规则进入设计校验器和 QA 门禁：

1. **独立碎片运动**不得由一个整体 Mesh 或整张图片旋转代替。
2. **持续燃烧**不得由一张完整火焰图片或一个静态 Ribbon 循环旋转代替。
3. **移动弹丸拖尾**必须由真实移动轨迹产生，不得使用固定在原地的静态线条冒充。
4. **模型附着特效**必须绑定到真实测试模型的 Transform、Socket、Mesh 或骨骼，不得只在模型旁放一个独立 Primitive。
5. **真实光照**必须包含实际 Light 或 Light2D，并证明场景接收物亮度发生变化；Additive Shader 不等于真实光。
6. **多阶段技能**必须证明状态之间有对象、方向、能量或残留的因果连续性，不得用无关静态层按 Alpha 窗口切换。
7. **3D 特效**不得仅凭一个面片倾斜角度来定义，必须至少包含空间运动、体积层、模型绑定或多视角成立中的一项正式要求。
8. 未满足必须载体时，应明确失败或降为 L2，不得在报告中写"基本等价"。

本目录是**已知作弊模式的枚举**，追不上新的作弊方式。对未枚举模式的补充防线是视觉 QA Agent 的开放式画面审查（能力边界见 6.3）和用户拒绝意见；两者发现的新模式必须回写进本目录、13 节门禁和 Human Verdict Corpus，使黑名单持续生长。

---

## 10. 项目专属 Design Skill

### 10.1 名称

候选名称：`unity-vfx-design-director`

### 10.2 作用

该 Skill 不直接负责生成 Unity 资产，而是负责：

- 访谈和澄清视觉目标。
- 从参考图提取可执行设计语义，并标注参考角色与权重。
- 输出 `VfxDesignContract`（含 requirement 分类与证据定位）。
- 选择合法载体组合；生成禁止替代列表。
- 生成关键帧、连续动作、验收清单和每条 visual requirement 的视觉判定描述。
- 在信息不足时停止，而不是编造默认设计。

### 10.3 计划中的源码结构

```text
docs/skills/unity-vfx-design-director/
├─ SKILL.md
├─ references/
│  ├─ carrier-matrix.md
│  ├─ semantic-patterns.md
│  ├─ quality-bar.md
│  └─ error-catalog.md
├─ schemas/
│  └─ vfx-design-contract.schema.json
└─ templates/
   ├─ VFX_DESIGN_CONTRACT.template.json
   ├─ VFX_IMPLEMENTATION_TRACE.template.json
   └─ VFX_VISUAL_REVIEW.template.md
```

视觉 QA Reviewer 同样以项目内源码形式落地：

```text
docs/skills/unity-vfx-visual-qa/
├─ AGENT.md              # 视觉审查子 Agent 的系统提示与工作流（版本化）
├─ review-protocol.md    # 证据输入格式、三态判定与三分类规则、报告格式、聚合规则
├─ calibration/          # S0a 校准集清单、盲测 Holdout 清单与指标报告
└─ cheat-patterns.md     # 与 error-catalog 同步的画面级作弊特征
```

`docs/skills` 作为项目内可版本控制的唯一来源；通过评审后，再决定安装到 Codex、Agent 或其他客户端的具体路径，避免本地安装副本成为第二权威。

### 10.4 硬性输出

Design Skill 每次只能输出以下结论之一：

- `DESIGN_READY`：合同完整，可交给实现者。
- `NEEDS_USER_DECISION`：缺少会改变结果的用户选择。
- `UNSUPPORTED_CARRIER`：设计所需载体当前系统不支持。
- `REFERENCE_CONFLICT`：参考之间存在互相冲突的目标。

视觉 QA Agent 的硬性输出只能是：

- `VISUAL_PASS`：全部 required visual 判定项 pass，可提交用户。
- `VISUAL_FAIL`：附逐条 fail 项、帧号和偏差描述，进入下一候选。
- `EVIDENCE_INVALID`：截帧缺失、分辨率不足、元数据/hash 不一致等证据问题，退回采集工具链。
- `CONTRACT_AMBIGUOUS`：合同视觉描述存在多个合理解释，退回设计。
- `VISUAL_UNCERTAIN`：真正的视觉歧义，升级用户。

禁止输出模糊的"应该差不多可以实现"或模型自报数字置信度作为主要依据。（1.3）异常情形按类别强制路由，不再混写在一个清单里：

- 判 `EVIDENCE_INVALID`：画面分辨率不足；关键区域被 Bloom、遮挡或裁切而无法判断；帧缺失、顺序错或元数据/hash 不一致。
- 判 `CONTRACT_AMBIGUOUS`：合同视觉描述存在多个合理解释；参考目标互相冲突；要求在约定证据定位中本来不可观察（属合同证据定位缺陷）。
- 判 `VISUAL_UNCERTAIN`：证据与合同均有效，但画面本身无法可靠区分（如 filmstrip 无法区分运动方向）。

**指标计数口径（1.3 明确）**：per-requirement 层只统计三态 `pass / fail / uncertain`；顶层路由（`VISUAL_PASS / VISUAL_FAIL / EVIDENCE_INVALID / CONTRACT_AMBIGUOUS / VISUAL_UNCERTAIN`）单独统计。`EVIDENCE_INVALID` 计算独立召回率；`CONTRACT_AMBIGUOUS` 在 S0a 首轮不设指标（除非专门制作对应校准样本并如实声明）。

---

## 11. Unity 技术底座

### 11.1 当前主路线

Unity 2022.3 URP 下先以以下原生能力为正式生产底座：

- Shuriken Particle System；Particle System Trails
- TrailRenderer / LineRenderer
- Procedural Mesh / Imported Mesh；MeshRenderer / SkinnedMeshRenderer
- 自定义 URP Shader / Shader Graph
- Light / URP Light2D
- Decal 或项目认可的地面残留方案
- Runtime 状态机、事件和对象池

### 11.2 必须新增的模块

1. **Sustained Effect Controller**：支持 start、steady loop、interrupt、stop、clear，保证持续火焰等效果可稳定运行并正确结束。

2. **Moving Emitter + Trail Protocol**：拖尾由真实运动产生，记录轨迹、清理和对象池复用行为。

3. **Model Binding Adapter**：支持 Transform、Socket、Renderer、Mesh、Skinned Mesh 和骨骼绑定。

4. **Fragment Motion System**：支持独立碎片位置、旋转、速度、阻尼、生命周期和确定性 seed。

5. **Real Lighting Module**：支持 3D Light 与 URP Light2D，默认关闭高成本阴影，具备数量、范围和亮度预算。

6. **Semantic Timeline / State Machine**：状态不是简单显隐，而是携带连续的对象、能量、方向、碰撞和残留。

7. **WYSIWYG Continuous Capture（1.2 按评审重定义）**

   **运行环境（硬性规定）**：视觉采集作业固定为 `Unity.exe -batchmode -projectPath <project> ...`，**明确不加 `-nographics`**（该参数不初始化图形设备，URP 渲染在其下不可用且项目历史已记录 `Camera.Render` 崩溃）。`tools/Invoke-Unity.ps1` 的 `-UseGraphics` 开关为视觉作业的必选项。使用正式预览场景的同一序列化相机，`Time.captureFramerate` 固定模拟步长，真实 Update / LateUpdate 连续采集；S0 使用同步 `ReadPixels`（可靠优先），Async GPU Readback 留作后续优化。

   **环境冻结**：Unity/URP 版本、图形 API 与显卡驱动标识、分辨率、RenderTexture 格式、sRGB/Linear、HDR、MSAA、Camera、Renderer Asset、Volume、Bloom、Tone Mapping、seed、fps、场景与 Prefab/Manifest hash——全部写入 Capture Profile 并随证据存档。

   **输出物（四路证据包中的前两路）**：
   - **Beauty Sequence**：合同关键帧 PNG + 分段 filmstrip（等间隔抽帧，覆盖完整生命周期）+ 3D 效果至少两个视角；供视觉 QA 与用户查看。
   - **Diagnostic Render Passes**：effect-only Mask、layer/object-ID、depth/normal、receiver 线性亮度（固定曝光的线性 HDR 缓冲）等机器诊断缓冲；只用于量测，不冒充最终画面。实现为诊断材质 / Renderer Feature / 受控替代 Shader。
   - **帧元数据与 manifest**：帧号、模拟时间、当前状态名、seed、source hashes（场景、Prefab GUID、Manifest/build hash、Capture 工具版本）、diagnostic pass manifest。证据目录 write-once。

   **存储策略**:全帧率原始帧只存系统临时目录、分析后删除；正式证据只保留关键帧、filmstrip、指标、hash 和必要的低码率预览。机器分析用全帧，视觉 Agent 用分段 filmstrip + 关键帧。

8. **Implementation Trace Recorder（Semantic Telemetry，四路证据第三路）**：自动记录设计要求对应的 GameObject、Component、Material、Shader、事件、状态、粒子数、Transform、Trail 顶点、Light 参数、绑定对象和预算读回。它是 behavioral / structural / budget requirement 的**权威证据**。

### 11.3 VFX Graph 的位置

VFX Graph 不是立即替换 Particle System 的前提。Unity 2022.3 官方文档明确其对 URP 与 URP 移动平台的完整支持仍在开发中，部分功能受限，因此只进行隔离 Spike，且不作为 S0 前提：

- 验证 URP 14.0.12 与目标平台兼容性。
- 验证 Windows Player Build、序列化、批处理和测试环境。
- 验证 Lit/Unlit、Trail、Depth、Distortion 等实际所需功能。
- 验证可否被编译器稳定生成、Patch 和回滚。
- 通过后只用于明确受益的大规模 GPU 粒子或复杂模拟。

---

## 12. 四条垂直生产基线

在以下四条基线达到 L4 前，不启动新一轮大规模类型生成。唯一例外（不登记为正式内容类型）：S0a 校准 mutants、测试夹具、为证明底座能力所需的最小诊断资产。

### 12.1 基线 A：持续燃烧火焰

必须包含：点燃阶段；稳态持续燃烧至少 3 个可观察循环；停止或中断阶段；火焰核心、外焰、烟、余烬；一个受预算约束的真实光照层；停止后粒子、光照和残留正确清理。

它验证 sustained lifecycle、粒子、Shader、Light、循环稳定性和停止协议，同时是 S0b 垂直切片的载体。**（1.2 明确）火焰内的 Light 只验证最小接口（存在、开关、接收物亮度变化）；完整真实光照质量留给基线 D，避免重复建设。**

### 12.2 基线 B：移动弹丸与真实拖尾

必须包含：发射；真实世界或局部空间移动；随运动产生的拖尾；命中；命中残留；对象池复用后无旧轨迹。

它验证 moving emitter、trail、事件、命中插槽和空间连续性。

### 12.3 基线 C：模型附着特效

候选为"武器附魔 + 武器挥砍拖尾"或"角色表面溶解"。必须使用真实测试模型（默认 Unity 官方示例模型或项目自带简易授权模型，用户可随时指定替换），并至少证明：正确绑定 Transform / Socket / Mesh / Skinned Mesh；模型运动后特效保持对齐；多视角观察成立；模型替换或丢失时有明确错误。

### 12.4 基线 D：真实光效

必须包含：一个短时命中或枪口光；一个持续火焰光；场景接收物；实际 Light / Light2D 读回；开灯前后接收物**线性亮度**差异；数量、范围、亮度和阴影预算。

它验证"画面很亮"和"真的照亮场景"被系统区分。

---

## 13. 机器门禁（1.2 按评审重构为"权威证据 + 交叉验证"）

### 13.0 证据权威原则（1.3 修正为 per-requirement 路由）

每条 requirement 必须显式声明 `evidenceAuthority`，指定唯一权威证据；其余证据只做独立交叉验证，权威与交叉矛盾即失败。不再机械要求"至少两层拒绝"（三层可能共享同一错误假设）。同时含内部事实与画面事实的复合要求（如"真实光照""停止清理""阶段连续性"）必须拆成多个 requirementId，各自唯一权威：

| `evidenceAuthority` | 适用 requirement | 权威证据 | 交叉验证 |
|---|---|---|---|
| `telemetry` | behavioral / structural 内部事实（碎片独立、拖尾顶点来源、绑定、状态序列、seed 确定性）与 budget（配合构建报告） | Semantic Telemetry（组件与状态读回） | Diagnostic Pass 量测、Beauty 帧 QA |
| `diagnostic` | **可量测**画面事实（层 Mask 可见性、接收物线性亮度变化、清理残差、Mask IoU 连续性表现） | Diagnostic Pass 量测（Mask/ID/线性亮度） | Beauty 帧视觉 QA、Telemetry |
| `visualQa` | **定性**视觉语义（主次与注意力中心、材质与混合统一、轮廓可读性、模板感、参考意图偏离） | Visual QA Agent（Beauty 帧必需，Diagnostic Pass 可选辅助） | 用户抽查、Human Verdict Corpus |
| `user` | 审美与商用品质 | 用户签署（Human Verdict Corpus） | 视觉 QA advisory |
| `calibrationLabels`（仅 S0a） | 校准样本 ground truth | 冻结、带 hash 的人工裁决清单 `calibration-labels.json`（Patch 注入信息只是初始标签与可追踪来源） | 注入参数记录 |

### 13.1 通用门禁

- 设计合同 Schema 合法；每个 Required Layer 存在合法载体。
- 每个 `designRequirementId` 都能映射到实现对象。
- 状态顺序、事件顺序和双出口符合合同。
- 同 seed 的轨迹和事件结果确定（canonical + 2 robustness seed）。
- 重复 Build 字节稳定。
- Preview 与证据使用同一正式场景和相机；证据 hash 与 Capture Profile 一致。
- 连续播放完成后按合同 `cleanupDeadline` 与 `allowedResidualLayers` 无非法残留对象、粒子、Light 或旧 Trail。

### 13.2a 组件语义门禁（Telemetry 权威）

- 独立碎片：多个独立 Transform 或粒子实例具有不同角速度/轨迹，而不是共同父对象单一旋转。
- 持续效果：稳态窗口内统计稳定，停止后在规定时间内归零。
- 拖尾：顶点由发射体真实位移产生，静止时不新增头部顶点，对象池重用前清理。
- 模型绑定：记录并验证绑定对象、骨骼或 Renderer，模型运动后锚点误差受限。
- 真实光照：必须存在 Light 组件，参数读回符合预算。
- 多阶段：相邻状态之间至少有一个合同指定的连续对象或数值，而非全部对象瞬间替换。
- 预算：粒子、Renderer、Material、Texture、Mesh、Light 全部产生报告。

### 13.2b 渲染量测门禁（1.3 更正：对**可量测视觉事实**为权威证据，对内部事实仅为交叉证据；Beauty 只交叉；按评审逐项降级）

工具落地在 `tools/vfx` 下，S0 只依赖 NumPy/Pillow（本机已有），**不为光流引入 OpenCV 等新依赖**；帧差、直方图、连通域、自相关均自建。各项定义：

- **碎片独立性**：权威证据为 Telemetry；渲染侧用 fragment-ID Pass 在屏幕空间跟踪各 ID 的质心、角度与轨迹相关性（整体旋转的各 ID 轨迹高度相关且符合单一刚体模型）。普通 RGB 光流不做 pass/fail 判据，至多输出诊断图。
- **循环稳定性**：对合同声明为周期性的素材用自相关/频谱检测；对随机稳态（如火焰）改为三个稳态窗口的 effect-Mask 面积、亮度分位数、质心、粒子数分布对比 + 线性漂移检验。门禁目标是"分布稳定、无长期漂移"，不是"必须严格周期"。
- **真实光照**：receiver-ID Mask + 固定曝光线性 HDR 缓冲，A/B 只切换 Light、不改变模拟状态，计算接收物区域线性 luminance 差并超过合同阈值；同时检查 Additive 主体区域之外的接收区，防止把特效本体的亮当成照明。Beauty PNG（受 Gamma/Tone Mapping/Bloom 污染）只供视觉 QA 观看，不做亮度判据。
- **停止清理**：合同先声明 `cleanupDeadline` 与 `allowedResidualLayers`；用 effect-only / 按层 Mask 与基准帧比较，判据为归一化 MAE + 残留连通域面积，不要求逐像素相等；合同允许的烟/Decal 残留层豁免。
- **拖尾真实性**：权威证据为 Telemetry（顶点来源于位移）；渲染侧用 trail-only Mask 的骨架与发射体历史投影比较走廊覆盖率 / 平均最近距离；静止期只禁止"头部向新空间增长"，允许尾部合法淡出收缩。
- **阶段连续性**：按合同每个 transition 的 `continuityMode` 分别判定——continuous 用逐层 Mask IoU、锚点位移、面积/能量变化率；impulse 允许大帧差但仍检查指定连续锚点或残留层；不使用统一的全画面帧差阈值。
- **多视角一致性（3D）**：结合载体读回与 Depth/Normal 或 object-ID Pass；合同要求空间体积时检查视角变化后的轮廓变化、深度跨度、遮挡关系或锚点视差。"两个视角都有像素"不构成 3D 证明；Billboard 为合同允许载体时不以"非 3D"拒绝。

渲染量测的输出只能是量测数值 + pass/fail，不得输出审美结论。

### 13.3 机器不得宣称的内容

测试不得输出"视觉已商用""构图优秀""质感达到参考图"。机器只能输出 `machine gates passed`。

---

## 14. 视觉审查门禁（视觉 QA Agent 先审，用户终审）

### 14.1 视觉 QA Agent 审查（用户验收前强制前置）

输入：设计合同、参考图（含角色权重）、关键帧、filmstrip、多视角帧、机器门禁报告。逐项检查：

- 每个 visual `designRequirementId` 的判定描述在其证据定位（状态/帧区间/ROI）上是否成立。
- 轮廓在游戏距离是否可读；视觉主次和注意力中心是否清楚。
- 动作是否有原因、过程和结果（在 filmstrip 上核查因果连续）。
- 各层亮度、色彩、材质和混合模式是否统一。
- 连续播放是否与关键帧设计一致。
- 是否出现整体图片旋转、切片接缝、穿帮、硬开关、静态假拖尾等问题。
- 是否仍像通用模板换色，而不是目标技能。
- 与参考图**被标注角色**的维度是否明显偏离（不审未标注维度，不要求像素级相似）。

审查报告必须逐条引用设计合同，禁止只看一张最好看的截图；每条 fail 必须附帧号与画面位置。

### 14.2 用户终审（1.3 修正：区分两类入口）

用户在正式预览场景中动态验收，检查商用品质、审美和游戏语境适配。用户入口分两类：

- **普通签署入口（L3 → L4）**：候选必须已获得 14.1 的 `VISUAL_PASS`。
- **带醒目标记的用户升级入口**：`VISUAL_UNCERTAIN` 升级、`S0A_ADVISORY_ONLY` 模式下的候选、`CAPTURE_BLOCKED` 与 C2 超限的 `NEEDS_USER_DECISION`、校准标签争议复核——这些不受"必须先 VISUAL_PASS"限制，但必须显著标记其来源与 QA 状态；advisory 报告不得伪装成 QA pass。

用户的签署与拒绝（原因、帧号、`contractRevision`）写入 Human Verdict Corpus，拒绝意见回写为新的视觉判定描述或新的作弊模式条目。

---

## 15. 既有资产重新盘点

### 15.1 原则

- 不立即删除；不否定已经完成的工程协议。
- 不把源码完成继续写成视觉完成。
- 用户已经明确签署过的结果保留签署记录。
- 所有改动继续受 owned outputs、GUID、依赖和事务回滚规则约束。

### 15.2 盘点步骤

1. 枚举所有正式 Runtime Entry、Recipe、Manifest、依赖和 Preview Scene。
2. 按编译器或模板家族抽样，识别"同一通用载体换色/换名"的条目。
3. 为每个条目标记 L0–L4。**（1.2 修正）视觉 Agent 批量初筛只输出风险分和抽样建议，不直接产生最终分级**——既有条目的合同、相机、生命周期与参考完整度不一致，最终 L 级由正式流程或用户抽样复核确定。
4. 对 L1/L2 写出缺失的设计合同、载体和验收证据。
5. 将"需要重做"分为设计缺失、载体缺失、实现错误、证据不足四类。
6. 只有用户批准迁移清单后才批量重建。

---

## 16. 外部模块研究与采用策略

### 16.1 可参考或可试验模块

| 模块 | 用途 | 当前建议 |
|---|---|---|
| [Coplay Unity MCP](https://github.com/CoplayDev/unity-mcp) | 让 Agent 操作 Unity、Shader、VFX 等 | 隔离 MCP Spike 首选，**排在 S1 之后**；它是执行工具，不是设计师。S0 只用已验证的批处理、Editor 脚本和测试工具 |
| [Unity Open MCP Skills](https://github.com/AlexeyPerov/Unity-Open-MCP/blob/master/docs/skills.md) | Particle、Lighting、Shader Graph、VFX Graph 的 Skill 示例 | 参考指令拆分，不直接作为生产依赖 |
| [Unity VFX Graph Samples](https://github.com/Unity-Technologies/VisualEffectGraph-Samples) | 官方 Bonfire、Ribbon、Skinned Mesh 等参考 | 作为技术样例库，逐项核对版本和许可 |
| [Unity VFX Toolbox](https://github.com/Unity-Technologies/VFXToolbox) | Flipbook、图像序列和 DCC 工具 | 需要序列处理时独立评估 |
| [Unity 2022.3 VFX Graph 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.visualeffectgraph.html) | 当前引擎版本兼容依据 | 决定 VFX Graph GO / NO-GO 的权威参考之一 |
| [Unity Particle Trails](https://docs.unity3d.com/2022.3/Documentation/Manual/PartSysTrailsModule.html) | 正式粒子拖尾能力 | 直接纳入当前主技术路线 |

### 16.2 不直接采用的候选

- 面向 Three.js / WebGL 的 VFX Skill：可以参考设计思路，但不能作为 Unity 生产依赖。
- 仅支持 Unity 6 的工具：当前工程为 Unity 2022.3，不接入主线。
- 无法稳定写入、回滚、测试或追踪图节点的 VFX Graph 工具：只做阅读参考。

### 16.3 采用门禁

任何第三方依赖进入主工程前必须完成：版本和 Unity 2022.3 兼容性验证；许可证与资产来源记录；Batch Mode / Player Build 验证；撤销和卸载方案；Generated 输出所有权和清理边界；对现有 Recipe、Patch、GUID 和事务系统的影响评估；用户明确批准。

---

## 17. 分阶段实施计划（1.2：S0 拆为 S0a / S0b）

### S0a：状态校正 + 受控错误样本 + 视觉 QA 盲测校准

范围：

- 既有资产状态口径校正：L0–L4 定义落入规则、暂停批量扩类型、冻结清单。
- WYSIWYG Continuous Capture 最小可用版（带图形设备 batchmode、Capture Profile、Beauty + 最小诊断 Pass、帧元数据与 hash）。
- 视觉 QA Agent 最小可用版（读帧、三态 + 三分类、结构化报告）。
- **校准夹具（mutants）**：为持续火焰制作受控变异，每个只注入一个明确错误，覆盖评审列出的 12 类（整体同步旋转、稳态漂移、循环接缝、停止粒子残留、停止 Light 残留、烟遮主体、层级颠倒、缺点燃、停止硬切、Additive 假光、相机/尺寸错误、证据缺失或元数据不一致）；每类含明显/边界强度与固定 seed，匿名随机 ID 提交，文件名不暴露答案。
- **生成方式与分阶段规模（1.3 按最终审查定稿）**：mutants 必须用参数化 Patch 脚本批量生成。顺序检验方案：① 先生成缩减集 **36 fail / 12 pass / 12 uncertain 边界 / 6 evidence-invalid**（与完整集同比例）；② 缩减集出现任何 false-pass 即停止扩样，先修 QA 协议；③ 缩减集 0 false-pass 则进入 `S0A_ADVISORY_ONLY`（见退出条件），由用户决定是否在 advisory 模式下推进 S0b；④ 增补 24/8/8/4 达到完整 Holdout **60 / 20 / 20 / 10**；⑤ 完整 Holdout 达标才进入 `S0A_GATE_QUALIFIED`。缩减集只能校验生成器、标签流程、输入格式与 QA 提示的基本可用性（36 个 fail 即使 0 false-pass，零事件估算的 95% 上界仍约 8%–10%），不得宣称达到完整集约 5% 的上界，不得单独授予 Visual QA 正式 L3 门禁权。
- **标签规则与标签权威**：注入错误提供初始 ground truth；最终标签权威是**冻结、带 hash 的人工裁决清单** `calibration-labels.json`（字段：sampleId、ground-truth 路由与 per-requirement 标签、标签来源与审核者、是否画面可见、requirementId、证据 hash、裁决版本与清单 hash；已列入 13.0 权威证据表）。错误在最终画面不可见时不得算视觉 fail——从视觉混淆矩阵剔除，转为 behavioral / structural 测试样本，不得为凑 fail 数保留；用户审核每类至少一个强失败、一个边界案例及全部争议标签。
- **冻结与盲测**：正式盲测前冻结 QA 提示与版本、模型版本与图像输入策略、合同版本、filmstrip 布局/分辨率/帧表、三态与聚合规则。校准集可用于改提示；盲测 Holdout 单独生成，不得再用于调提示。

退出条件——S0a 只有两个合法终态（1.3 定稿），二者都算"S0a 完成"，但权限不同：

- **`S0A_GATE_QUALIFIED`**：完整 Holdout（60/20/20/10）达到全部指标门槛，Visual QA 获得普通 L3 门禁权。
- **`S0A_ADVISORY_ONLY`**：盲测完成但样本不足（仅缩减集）或指标未全达标；QA 报告必跑但不阻断，不能生成普通 L3；进入 S0b 需用户显式授权，候选走 14.2 的带标记升级入口。

指标门槛（完整 Holdout 口径；per-requirement 与顶层路由分开计数，见 10.4）：

- **per-requirement 三态层**（只统计 pass / fail / uncertain）：已知视觉 fail 的 **false-pass = 0**；已知 pass 的 false-fail ≤ 10%；非边界集 uncertain ≤ 15%；报告按 `designRequirementId` 类型拆分的混淆矩阵。
- **顶层路由层**（五路输出单独统计）：`EVIDENCE_INVALID` 对注入样本（完整集 10 个 / 缩减集 6 个）的独立召回率 100%；`CONTRACT_AMBIGUOUS` 首轮不设指标（未制作专门样本时如实声明不评）。
- **稳定性**：同一证据 3 个全新隔离会话重复审查，per-requirement 三态一致率与顶层路由一致率均 ≥ 90%。

### S0b：正式持续火焰垂直切片

范围：

- 基线 A 的最小合同（7.9 十段；先冻结 Capture Profile 和参考角色，再写实现）。
- Sustained Effect Controller。
- 实现 → 采集 → 机器门禁 → 视觉 QA → 修复的 C0/C1/C2 回路实际跑通，直到基线 A 达到用户签署 L4。

退出条件：

- 用户批准状态口径（若 S0a 未单独批准）。
- 基线 A 由用户动态签署 L4（绑定 contractRevision + buildHash + captureProfile）。
- 产出《切片复盘》：最小合同缺了哪些字段、视觉 QA 盲测与实战判对/判错对比、各诊断 Pass 与量测项的实际效用、回路平均候选数。**这份复盘是后续所有基建的需求来源。**

### S1：从切片经验定稿合同与门禁框架

产出：`VfxDesignContract` 完整 Schema（以 7.9 为核、按复盘扩展）；Design Director Skill 正式版；视觉载体矩阵、语义模式库、错误替代目录；合同结构和语义 Validator；Implementation Trace 正式版；渲染量测工具集（13.2b，按复盘验证过的量测项落地）；视觉 QA Agent 正式版与审查协议；机器/人工验收报告模板、L1–L4 状态写回规则。

退出条件：

- 同一个需求由不同执行者读取时，关键视觉层、状态、载体和禁止替代结论一致。
- 故意提交"整体旋转假碎片""静态假拖尾""Additive 假光"等实现时，**该要求的权威证据层必判失败，且至少一个独立交叉证据层同时报警**。

### S2：其余底座模块

产出：Moving Emitter + Trail Protocol；Model Binding Adapter；Fragment Motion System；Real Lighting Module；Semantic State Machine；Capture 与 Trace 的完整版（含全部诊断 Pass）。

退出条件：每个模块都有纯逻辑、EditMode、PlayMode 和 Player Build 证据。

### S3：其余三条垂直基线

依次开发（正式链路：合同 → 实现 → 候选包 → 四路证据 → 机器门禁 → 视觉 QA → 用户签署）：

1. 移动弹丸和真实拖尾
2. 模型附着特效
3. 真实光效

退出条件：四条基线（含 S0b 的火焰）全部达到 L4；任一条未通过都不得开始批量迁移。

### S4：既有资产审计与迁移

产出：既有条目分级报告（视觉 QA 批量初筛输出风险分 + 用户抽样复核定级）；重做优先级；通用载体过度复用清单；用户批准的批量迁移批次。

退出条件：用户批准保留、重做、废弃和延后清单。

### S5：恢复原计划扩展

在新生产链下继续元素、风格、Archetype 和组合内容；所有新条目先有合同，再实现，再过视觉 QA，不再先批量生成后补设计。

### S6：Standalone Desktop、Unity Worker 与安全 Broker（1.4）

旧 Unity Editor UI 已冻结为兼容/诊断基线，不再扩展五标签页、Player UI、美化或嵌入式 MCP 入口。S6 改按以下六阶段执行：

1. Phase 0：ADR、文件级计划、旧 UI→Desktop 迁移矩阵、协议 ownership、威胁模型和失败关闭顺序；独立审计 P0/P1=0 后才开新代码根。
2. Phase 1：`.NET 8 + Avalonia + MVVM` Desktop shell、纯 C# Shared Protocol、Client；Unity/Broker 缺失时独立启动并明确 disconnected，零 listener、零工程写入。
3. Phase 2：Broker-owned 项目注册、authenticated named pipe 和 pinned native handle/capability；Desktop 不能提交任意绝对路径。
4. Phase 3：Unity Worker 版本化命令与 Jobs（validate、build candidate、preview、focused tests、cancel），事务、幂等和崩溃恢复。
5. Phase 4：独立 Preview/Review/Evidence；媒体与证据身份、Desktop 像素/可访问性和 authority 分离分别取证。
6. Phase 5：功能对等、安装升级/回滚与全链 E2E；用户确认后旧 UI 才可隐藏，仍不立即删除。

当前执行状态：Phase 1 有界门禁通过，独立最终审计为 `P0=0 / P1=0 / P2=5`；Phase 2 的 protocol/Broker/Worker-codec/handle-admission foundation、test-only Broker pipe loop、实际 Unity 生命周期、handle-relative reader，以及 r11 Broker→Unity test-pipe fixed read 已启动，但 production connection 仍 `NO_GO`，Phase 3–5 未启动。Phase 1 的断连协议/客户端/结构性桌面壳收据不证明连接；Phase 2 的 native handle probe、r48 Worker codec/admission、.NET test-peer pipe、r11 GUID scratch read-transport 收据都不是 authenticated production Unity transport、Client/Desktop route 或真实注册项目的 end-to-end read receipt。它们均不授予 production transport、Worker action、视觉结论、用户签署、L3 或 L4。

Desktop 不直接写 `Assets/Packages/ProjectSettings`；Worker 独占 Unity API。Broker/Worker 未冻结前继续 fail closed，禁止用公网 HTTP、任意 TCP、production stdio MCP、caller path、`EditorPrefs` 或环境变量绕过 W24FS001。AI 聊天仍非当前优先目标。

---

## 18. 相对规模与依赖

| 阶段 | 相对规模 | 依赖 |
|---|---:|---|
| S0a | 中（校准夹具 + 盲测） | 用户批准状态口径与本计划 |
| S0b | 中（一条真实基线打穿到 L4） | S0a 终态（`GATE_QUALIFIED` 直接进入；`ADVISORY_ONLY` 需用户显式授权） |
| S1 | 中 | S0b 复盘 |
| S2 | 大 | S1 |
| S3 | 大 | S2；每条需要用户动态验收 |
| S4 | 大 | S3 全部 L4 |
| S5 | 很大、分批执行 | S4 |
| S6 / Desktop Phase 0–5 | 很大（完整 production 范围约 3–5 个日历周，不含视觉返修） | 生产链稳定；Phase 0 审计后分阶段解锁 |

1.4 的范围重估：总体约 65%–70%；Desktop + Unity Worker 可用里程碑约 2–4 个日历周；包含 production Broker、安全门禁、安装与完整验收的累计里程碑约 3–5 个日历周；最终视觉返修另计。详细顺序估算为 Phase 0–5 的 `1–2 / 2–4 / 3–5 / 5–8 / 4–6 / 5–8` 天，合计 20–33 个日历日。阶段门禁顺序不可用并行开发压缩；同阶段内的独立文档、测试和审计工作可在不越权时并行。该估算不改变逐阶段机器门禁和用户视觉签署条件。

---

## 19. 风险与应对

### 19.1 设计合同过重

风险：制作小效果也要填写大量字段。
应对：S0 只用十段最小合同（其中多段为一次性冻结的环境配置，不逐效果重写）；完整版提供 Small / Standard / Hero 三档模板，但禁止删除状态、载体和禁止替代等核心字段。

### 19.2 AI 仍会误解视觉语义

风险：Skill 不能保证审美，视觉 QA Agent 也会判错。
应对：QA 判定权限以 S0a 盲测指标为前提，达不到 false-pass=0 就降为 advisory；判定不确定时三分类分流而非猜测；用户拒绝意见持续回写；最终签署权始终在用户。

### 19.3 视觉 QA 回路空转

风险：实现者与视觉 QA 反复退回，消耗大量轮次不收敛。
应对：C0/C1/C2 三候选上限；根本冲突提前退设计；`EVIDENCE_INVALID` / `CONTRACT_AMBIGUOUS` 各回自己的责任层，不消耗候选；超限强制 `NEEDS_USER_DECISION` 并附全部候选的截帧对比。

### 19.4 截帧证据造假或失真

风险：错误相机/场景、挑帧、挑 seed、覆盖证据。
应对：采集只能由 Capture 工具链按合同帧表自动执行；场景、相机、Prefab GUID、Manifest/build hash、工具版本、帧号、模拟时间、seed 全部校验；证据目录 write-once；canonical + 2 robustness seed；QA 校验元数据一致性，不接受手工投喂的单张截图。

### 19.5 S0a 校准集成本失控

风险：110 个受控 mutants 手工制作让校准阶段本身变成大工程。
应对：mutants 必须参数化 Patch 脚本生成；脚本化不可行时按 17 S0a 的缩减规则起步并声明置信度；校准集资产不登记为正式内容。

### 19.6 Unity 2022.3 URP 限制

风险：部分 VFX Graph、Decal、Light2D 能力受 Renderer 和版本影响；`-nographics` 下无渲染。
应对：视觉作业固定带图形设备 batchmode；原生 Particle System 为主，VFX Graph 隔离验证；不先迁移再发现不兼容。

### 19.7 第三方工具污染工程

风险：包、缓存、菜单、生成文件和版本依赖进入主线。
应对：隔离项目或分支 Spike；通过安装、卸载、构建和所有权审计后再提审；MCP 排 S1 之后。

### 19.8 追求参考图导致资源膨胀

风险：用大 PNG 或完整序列帧换取短期视觉提升。
应对：合同必须先拆分可复用纹理、程序 Shader、粒子、Mesh 和 Light；同时报告源 PNG、本地独占和完整驻留体积。

### 19.9 批量迁移造成历史资产破坏

风险：GUID、依赖、Preview 或用户已通过资产被覆盖。
应对：Manifest 所有权、事务快照、稳定 GUID、逐批迁移和用户批准清单继续作为硬门禁。

---

## 20. 交付物

计划全部完成时应交付：

1. 项目专属 `unity-vfx-design-director` Skill 源码。
2. 项目专属 `unity-vfx-visual-qa` 视觉审查 Agent 源码、审查协议、校准集与盲测指标报告。
3. `VfxDesignContract` Schema、Validator 和模板（含 requirement 分类、视觉判定描述与证据定位字段）。
4. 视觉载体矩阵、语义模式库和错误替代目录（含画面级作弊特征）。
5. 三角色责任与验收权限规则。
6. 四路证据包格式：Beauty Sequence、Diagnostic Render Passes、Semantic Telemetry（Implementation Trace）、Human Verdict Corpus。
7. 渲染量测工具集（Mask/ID/线性亮度量测、分布稳定性、清理差分等，NumPy/Pillow 实现）。
8. 七个 Unity 技术底座模块（含带图形设备的 Capture 管线与诊断 Renderer Feature）。
9. 四条达到 L4 的生产基线与 S0b 切片复盘报告。
10. 既有资产 L0–L4 盘点（风险分初筛 + 用户复核）和迁移清单。
11. 外部 MCP / VFX Graph Spike 报告与 GO / NO-GO 结论。
12. 回写后的总规则、经验和最终生产手册。
13. 独立 Desktop、Shared Protocol、Client、可选 Broker、Unity Worker API、安装/升级/回滚与端到端恢复证据；旧 Unity UI 的功能对等迁移矩阵。

---

## 21. 完成定义（1.4 Standalone Desktop 增补）

W24 只有在以下条件全部成立时才算完成：

- 100% Required 设计要求能追踪到真实实现对象，并按各自 `evidenceAuthority` 取证（1.3 修正）：可量测视觉要求有 Diagnostic Pass 判定 + Beauty/QA 交叉；定性视觉语义要求有 Visual QA 判定 + Beauty 帧证据；behavioral / structural / budget 要求有遥测与构建证据；审美/商用要求由用户签署——不要求预算、seed、事件、GUID、对象池清理等非视觉要求由截图判断，也不要求定性语义由 Mask 量测判断。
- 禁止替代规则在**冻结的已知作弊测试集（frozen known-cheat corpus，含 S0a 校准集与后续回写的新模式样本）上 0 漏检**：每条规则的权威证据层必判失败，且至少一个独立交叉证据层同时报警。此结论不构成对未来未知作弊方式的绝对保证；新发现模式按 9 节回写后纳入测试集。
- 视觉 QA Agent 达到 `S0A_GATE_QUALIFIED` 并保持版本化（W24 最终完成不接受长期停留在 `S0A_ADVISORY_ONLY`）。
- 四条垂直基线全部通过机器门禁和视觉 QA 审查，并由用户动态签署 L4（绑定 contractRevision + buildHash + captureProfile）。
- 用户验收的每个候选都已先经过视觉 QA——普通签署入口必须取得 `VISUAL_PASS`；带醒目标记的升级入口可携带非 Pass 状态，用户不再是第一双看到画面的眼睛。
- 持续火焰、移动拖尾、模型附着和真实光照不再是缺失能力。
- 设计合同、运行结果和验收报告对同一状态使用相同术语。
- 不再用单张最佳截图替代连续动作验收。
- 不再用"功能存在"冒充"视觉可商用"。
- 未经用户批准的旧资产没有被删除或批量覆盖。
- 独立 Desktop 能在 Unity 未启动时安全显示 disconnected；通过可信 Broker 注册后完成连接、只读查询、Worker 作业、Preview/Review、断连恢复和安装升级门禁。
- Desktop 不直接写 Unity 工程；生产 real-read 和命令执行只来自通过独立安全审计的 Broker/Worker capability。
- 旧 Unity UI 在功能对等和用户确认前保持兼容/诊断可用；旧收据没有冒充 Desktop、Broker、IPC、installer 或新 Worker 证据。

---

## 22. 用户已批准的实施决策（1.4 当前授权基线）

以下内容已由用户于 2026-08-25 明确批准，按阶段退出门禁实施：

1. 建立项目专属 `unity-vfx-design-director` Skill + `unity-vfx-visual-qa` 视觉审查 Agent 组合。附加条件：Design Skill 不冒充审美模型；Visual QA 必须有版本化协议、校准集、盲测结果和已知能力边界，并以全新隔离上下文审查。
2. 采用 L0–L4 状态，未经视觉签署的历史资产如实归入 L1–L3（未经视觉 QA 看帧的最多 L2）。附加条件：L4 绑定 `contractRevision + buildHash + captureProfile`，重建即过期回 L3。
3. 四条垂直基线达到 L4 前暂停批量新增特效类型。唯一例外：S0a 校准 mutants、测试夹具、最小诊断资产，均不登记为正式内容。
4. 垂直切片顺序：S0a（校准盲测）→ S0b（持续火焰打穿 L4）→ 定稿完整 Schema 与其余基建；四基线顺序保持：持续火焰 → 移动弹丸拖尾 → 模型附着 → 真实光效；火焰内 Light 只验证最小接口，完整光照质量留给基线 D。
5. 回路硬规则：最多 3 个不可变候选 C0/C1/C2；机器门禁失败（`MACHINE_FAIL`）不进 QA 直接消耗候选；截帧只能由 Capture 工具链自动产出并带 hash 验证；uncertain 三分类，只有 `VISUAL_UNCERTAIN` 经带标记入口升级用户；合同变化重开 `contractRevision`，不计普通修复，连续第二次重开需用户确认。
6. 在隔离环境中评估 Coplay Unity MCP，排在 S1 之后，不直接安装到正式项目。
7. VFX Graph 只做 Unity 2022.3 URP 兼容 Spike（URP 14.0.12、Windows Player、实际所需功能、编译器生成/Patch/回滚/批处理捕获），通过后再决定是否纳入正式载体。

8. S0a 校准集分阶段规模规则（1.3 已按最终审查"有条件接受"方案定稿）：缩减集 36/12/12/6 → 最多 `S0A_ADVISORY_ONLY`；补齐 60/20/20/10 且全部指标达标 → `S0A_GATE_QUALIFIED`；缩减集不授予 L3 门禁权；`ADVISORY_ONLY` 下进入 S0b 仍需用户显式授权。
9. S6 最终形态改为独立 Desktop + Unity Worker + 可选独立安全 Broker。Unity Editor UI 立即停止新增功能并冻结为兼容/诊断层；Desktop/Broker 新证据必须独立取得，production registration/real-read/transport 在可信 issuer 前继续 fail closed。

本文件现为 W24 Goal 的授权执行基线；阶段状态、证据与未满足门禁必须在对应阶段报告中如实记录。
