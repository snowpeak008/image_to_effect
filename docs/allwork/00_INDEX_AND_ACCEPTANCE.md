# Allwork 总览：验收结论与后续开发总计划

> 日期：2026-08-26
> 定位：本目录是**本轮扩展开发的唯一顺序依据**。通常按文件编号从小到大逐个开发；W24 S6 现由 1.4 STOP-THE-LINE 的 Desktop Phase 0–5 门禁覆盖旧编号顺序。每个文档仍是可独立开工、独立验收的工作包（W 系列）。
> 上游规则不变：`docs/rules/00–70` 全部继续生效；Recipe/模板/编译器协议不推翻，只扩展。

---

## 1. 前序验收结论（本轮开工前提）

### 1.1 已通过

| 范围 | 证据 | 结论 |
|---|---|---|
| S1–S11 内部 MVP 0.1.0（fireball 2D/3D、Recipe v1、Patch、Runtime、A1–A8） | `docs/release/MVP_ACCEPTANCE_REPORT.md` | 通过（Gate E = GO） |
| S12 Slash v2（Recipe v2 解析/校验/编译/Runtime、AI Recipe 与 Patch 冻结证据） | `docs/stage-notes/S12*` | 工程闭环通过；最终视觉结论仍由用户在 W0 签署 |
| 规则体系（核心规则、Archetype 配置、资产命名、验收、机器强制、工程经验） | `docs/rules/00–70` | 已建立并持续回写 |
| 类型覆盖扩展：约 30 个 Generated 特效族，覆盖 Projectile / Impact / Slash / Aura / Area / Beam / Trail / Shield / Spawn / Transform / Composite / Environment / Screen-UI / 交互组合 | `project/Assets/VFX/Generated/`、`project/Assets/VFX/Recipes/` | 已产出 |

### 1.2 文档状态待回写（W0 处理，不阻塞能力开发）

以下报告的最终状态字段仍停在"等待用户视觉验收"。**验收权属于用户，不属于执行 Agent，也不能由机器测试替代。** 按 2026-08-24 的最终顺序决定，所有需要人眼判断的项目统一留到能力与内容开发之后逐项验收；用户签署通过后才能回写关闭，拒绝项按 03 号文档移交重做：

1. `docs/stage-notes/S15_VISUAL_DELTA_AND_TECHNICAL_PLAN.md` — Slash 视觉重建，标 "awaiting visual review"（S14 视觉验收已撤回）。
2. `docs/vfx-reviews/validation-gallery-3x3/` — 2D 九宫格 V2，"等待用户视觉验收"。
3. `docs/vfx-reviews/coverage-gallery-b/` — 空间覆盖九宫格第四轮 Screen/UI，"等待用户动态验收"。
4. `docs/vfx-reviews/interaction-gallery/` — 交互九宫格第三轮，"等待用户在 Unity 九宫格动态验收"。

### 1.3 明确短板（本轮扩展的立足点）

- **UI 几乎为零**：只有 `Editor/UI/VfxCompilerWindow.cs`（141 行，Tools/VFX Composer/Compiler），没有特效库浏览、搜索、风格选择、参数面板、内嵌预览、Patch 界面。
- **风格维度单一**：所有特效实际上只有一种 stylized 手调风格；Recipe 没有风格字段，风格无法复用与切换。
- **元素覆盖稀疏**：火/霜/电各一两个，水、风、岩土、自然、圣光、暗影、奥术等主流元素族完全缺失。
- **2D 风格纵深不足**：没有像素风、手绘帧动画风、水墨风等 2D 游戏最常用的风格路线。
- **3D 风格纵深不足**：没有写实向（半写实烟火）、全息科幻向的材质/网格路线。

---

## 2. 开发顺序总表

### 2.A W24 授权后的最高优先级覆盖

2026-08-25，用户授权 `24_VFX_DESIGN_TO_IMPLEMENTATION_SYNC.md` 1.3 作为当前最高优先级生产质量重构。2026-08-26 的 1.4 STOP-THE-LINE 决策把 S6 最终形态改为“独立 Desktop 主界面 + Unity Worker + 可选安全 Broker”；旧 Unity UI 冻结为兼容/诊断基线。原顺位 1–8 的源码与机器门禁结论保留；后续“全项目开发推进、视觉最后集中验收”授权允许继续实现新候选，但在 W24 四条垂直基线达到 L4 前，不得把它们批量晋升为正式商用品质、Publication 或 L4 内容。

当前执行顺序固定为：`W24 S0a → S0b → S1 → S2 → S3 → S4 → S5 → S6`。用户拥有所有视觉 L4、旧资产保留/重做/废弃以及 `S0A_ADVISORY_ONLY` 是否进入 S0b 的裁决权。

用户于 2026-08-25 指定视觉验收延后集中执行：各阶段源码与机器门禁完成后以 `VISUAL_PENDING` 连续进入下一开发阶段，不等待中途人工签署；全部视觉条目最终集中交用户验收。该安排只解除开发等待，不授予 L4，也不改变最终完成条件。

当前 W24 机器进度：S0a reduced66 已完成正式捕获、无答案投影和三次独立 advisory
Visual QA（尚无人类标签/校准终态）；fresh full110 原始捕获已 110/110 write-once 封存并完成
机械复核，但因尚未提供精确、不可变的 QA `model-version-id`，正式投影、QA session、corpus、
metrics 与 score 均未创建；S0b 已 `FORMAL_EVIDENCE_BOUND`；S1 合同门禁
`15/15`；S2 r17 current-byte 四门 `10/10`，专用 Windows Player build + 五场景运行的
machine evidence 已完成；S3 三基线合同 `19/19`、Runtime `6/6` 与三条 graphics-backed
typed evidence 均已完成；S5 当前 gate `26/26`；S6 r36 的 dormant registration `6/6`、
Windows 只读 scaffold `36/36`、Envelope/Inspector `41/41`、Studio Models `9/9`、Editor
callback/integration `12/12`，合计 `104/104`，均由隔离 Unity 自然 `exit 0` 验证。Production real-read/可信项目注册、外部 transport、
视觉/Player 与 authority 仍保持关闭；上述结果均不授予视觉通过、用户验收、L3 或 L4。

当前新架构进度为 **Phase 1 有界门禁通过；Phase 2 仅安全基础切片进行中，production connection 仍 NO-GO**。Phase 1 的纯 C# Protocol、disconnected Client 与 Avalonia Desktop shell 收据及 `P0=0 / P1=0 / P2=5` 审计结论保持不变，严格限定为 `DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY`。后续 Phase 2 foundation 已增加十类 exact wire contract、dormant Broker、OS peer facts、native root identity、跨进程只读句柄测试与有序 grant/ACK/revoke/ACK 状态机；发布后的裸句柄号不再由 Broker 强关。Unity Worker 的严格 grant/revoke codec 与 test-only opaque 三句柄 owner 已分别闭合，r48 exact filters 为 admission 13/13、protocol 6/6；无测试宏的独立 Editor 编译在 Worker namespace 内不含 ACK、named test issuer 或 hooks。r6 以实际 Unity 2022.3 Editor 进程跑通 test-only 生命周期 2/2，read-query r4 以 test-issued lease 跑通四类 handle-relative 读取 14/14，r11 再把两者接成 HandleProbe→实际 Unity 的五次 test-pipe 查询并获独立 scoped GO。最新 .NET r5 新增无路径 Client 查询编排与第二条 authenticated Desktop 测试管线，Protocol 80、Client 12、Desktop 9、Broker 35，合计 136/136；r2 因响应发布竞态/递归清理被拒，r3 又因 session-reservation/registry-lock 反序和 child-first cleanup 被拒，r4 虽闭合锁序却仍保留路径删除 TOCTOU、production-compiled test barrier 与非聚合 fixture cleanup。r5 使用两阶段 session invalidation/drain、确定性 mid-acquisition revoke、无产品 hook 的真实 reservation 测试以及 pinned-handle/no-follow、对象绑定的 bottom-up 清理闭合这些问题。它仍只使用 private test adapter 和同进程测试策略，并以 `P0=0 / P1=0 / P2=0` 获得 `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY` scoped GO。production connector/session/ACK issuer 仍关闭，HandleProbe 不可发布；production policy/global process-ownership、trusted ACL、Client/Desktop pipe connector、Desktop 展示与真实注册项目读取仍未实现，Broker 入口继续在 listener 前返回 `W24FS001`。详见 Phase 1、Phase 2 foundation、各 Worker/Broker 报告以及 `W24_PHASE2_DESKTOP_BROKER_READ_TRANSPORT_REPORT.md`。架构切换后的总体完成度仍按用户重估为约 65%–70%。旧 r31/r32/r35/r36 只能作 Unity 兼容基线，不能冒充 Desktop、Broker、IPC 或新 Worker 证据。

> **顺序修订（2026-08-24，第二版）：开发顺序以本表"开发顺位"列为准，不再等于文件编号。**
> 修订原则：**先补齐能力，再补齐风格**。行为机制（追踪/贯穿/反射/预警爆发等）是不同的模板与运动协议，是系统能力；元素与颜色只是变体 Recipe，是皮肤。因此能力批次（20–23 号文档）插到元素批次之前；元素/风格批次改为"给能力素体套皮"，工作量下降、且不再重复造运动逻辑。

### 2.0 开发顺位（执行按这个走）

| 顺位 | 文档 | 说明 |
|---|---|---|
| 1 | 20（能力矩阵总纲）→ 21（弹道能力）→ 22（射线能力）→ 23（时序/范围能力） | **本轮第一优先级：behavior 块 + 30 个能力素体 + 采样验收机制** |
| 最终视觉阶段 | 03（W0 遗留闭环）以及各批次视觉签署 | 所有视觉验收集中到最后；只能由用户签署 |
| 3 | 01（风格底座）、02（Studio UI，其 Library 增加"按能力浏览"维度） | 与能力批次可部分并行（01 的 style 块与 20 的 behavior 块同属 Schema 升级，合并一次迁移） |
| 4 | 16（新 Archetype） | 类型扩容，部分依赖能力协议（WeaponTrail 端点、Loot 拾取端点） |
| 5 | 04–09（六大元素族） | 套皮为主：规格卡增加「引用能力」列，行为一律引用能力 token |
| 6 | 12（环境）、13（打击感）、15（屏幕）、18（游戏交互 UI） | 独立内容线 |
| 7 | 10、11、17（风格专项与风格包二） | 风格纵深，最后补 |
| 8 | 14（大招组合）、19（角色套装） | 综合编排，依赖全部前序 |

### 2.0.1 当前执行状态（2026-08-25）

- 原计划源码与机器门禁保持完成。最终视觉状态为 **24/25 已由用户签署（4 项有条件通过、20 项拒绝）**；仅 W2 Studio UI 按用户要求暂时搁置且仍待签署，因此不得声明“全计划视觉完成”。详细结论见 `docs/stage-notes/FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md`。

- **W-C0–W-C3 机器开发已完成**：一次性完成兼容 Recipe v1 的 `behavior` + 预留 `style` 1.2 契约迁移；登记能力 token、组合规则、纯逻辑采样、事件契约、确定性、视觉槽和双出口协议。
- 已落地 12 个弹道、8 个射线、10 个时序/范围素体；另有 2 个视觉槽支撑 Runtime Entry。三个批次 Preview Scene 已生成；用户已分别拒绝 W-C1、W-C2、W-C3 的当前视觉候选，机器能力完成不折算为视觉通过。
- 2026-08-25 后续 W24 全项目开发授权下，W-C1、W-C2、W-C3 已分别另建 `next_candidate / VISUAL_PENDING`。W-C1 的 Split 5 子核、四跳严格时序、Volley 三阶段真实载体通过 EditMode `7/7 + 1/1`、PlayMode `2/2`；W-C2 的 hitscan 衰减、端点跟随、真实 sweep、三级 charge、反射折线、显式遮挡、四源汇聚与顺序 arc-link 通过 EditMode `5/5 + 1/1`、PlayMode `9/9`；W-C3 的预警/延爆/Tick/三级蓄力/双出口/五点连锁/扩缩环/移动残留/成长脉冲真实载体通过 EditMode `6/6`、PlayMode `5/5`、Preview `1/1`，并再次通过 W-C1 `7/7 + 2/2 + 1/1` 与 W-C2 `5/5 + 9/9 + 1/1` 共享回归。旧候选拒绝记录不变；三个新候选都尚未由用户视觉签署。
- 全量回归：EditMode `193 total / 158 passed / 0 failed / 35 historical Explicit skipped`；PlayMode `22/22`；详见 `docs/stage-notes/WC0_WC3_CAPABILITY_REPORT.md`。
- `style` 字段本轮只完成 Schema、解析和校验预留；风格渲染实现及“能力+皮肤=成品”的最终视觉签署属于顺位 3 的 W1 与最终视觉阶段，不在本次机器能力完成结论中冒充完成。
- **顺位 3 W1/W2 与顺位 4 W15 的旧候选已完成源码和机器门禁**：W1 与 W15 旧视觉候选已被用户拒绝；W2 Studio UI 暂时搁置且仍待签署。其后另行授权的 W1 `next_candidate` 已完成隔离构建，实际通过 EditMode `3/3`、Preview `1/1`、PlayMode `4/4` 及 W-C1/W-C2/W-C3、旧 W1、W3+ 定向共享回归；当前仍为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`，不折算为旧拒绝翻案或用户视觉通过。详见 `W1_W2_STYLE_STUDIO_REPORT.md`、`W1_NEXT_CANDIDATE_REPORT.md` 与 `W15_NEW_ARCHETYPES_REPORT.md`。
- **顺位 5 W3–W8 已完成源码和机器门禁**：六大元素批次共 47 份默认 Recipe、47 条稳定语义 Patch、4 份风格变体、51 个 strict Runtime Entry 与 6 个批次 Preview Scene 已落地；全量 EditMode `206 total / 171 passed / 0 failed / 35 historical Explicit skipped`，PlayMode `30 total / 24 passed / 0 failed / 6 Explicit skipped`。用户已拒绝 W3，并通过“后续特效类不再逐 Scene 验收”的批量签署拒绝 W4–W8；未授权重做。详见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。
- **顺位 6 W11/W12/W14/W17 与顺位 7 W9/W10/W16 的旧候选已完成源码和机器门禁**：用户已通过后续特效类批量签署拒绝这七个当前视觉候选；相关旧 Scene 未作逐条/逐帧视觉核对。其后 W24 全项目授权下，W9/W10/W16 已新增隔离的 32 项 `next_candidate` 源码、三份 suffixed Preview 目标和 `4 + 1 + 4` 定向测试；W17 也已新增 10 项真实 Canvas 交互载体、固定 12 槽奖励池、三尺寸按钮与独立硬裁剪 Preview 源码。两组当前均仅完成静态门禁，状态为 `NEXT_CANDIDATE_VISUAL_PENDING`，不折算为旧候选翻案或用户视觉通过。W11/W12 同期另建 7+7 个独立环境天气/命中反馈源码候选、两个 suffixed Preview 目标与 `4 + 1 + 7` 定向测试；四层 focused Roslyn 静态编译通过，尚未执行 isolated Unity build/Test Runner，状态同为 `NEXT_CANDIDATE_VISUAL_PENDING`。
- **顺位 8 W13/W18 已完成源码和机器门禁**：6 个大招/Boss Composite、8 个角色专属条目、百鬼夜行组件和 4 个角色套装 Showcase 已落地；用户已通过后续特效类批量签署拒绝两个当前视觉候选。其后 W24 全项目授权下，W18 已新增 4 套独立 palette/形状 Mesh+Line、完整技能阶段、外部 rig 挂点、预算硬上限与 shader 裁剪 Preview 源码候选；当前仅完成静态门禁，状态为 `NEXT_CANDIDATE_VISUAL_PENDING`。W13 同期另建 6 个只读依赖池 Composite 源码候选、独立 suffixed Preview、play/stop timeline、三类 camera hint 与双 named gate；其 W13-only Build、Edit、Preview 与两条 Play 方法门禁已在 canonical-dependency overlay 下通过，独立审计为 P0=0/P1=0。2026-08-26 已将严格白名单内 30 个全新 W13 生成文件晋升到 canonical，逐文件与已审 shadow mismatch 0；旧拒绝和旧资产保持不变，状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`。详见 `docs/stage-notes/W13_W18_COMPOSITE_HERO_KITS_REPORT.md`、`docs/stage-notes/W17_W18_NEXT_CANDIDATE_REPORT.md` 与 `docs/stage-notes/W11_W13_NEXT_CANDIDATE_REPORT.md`。
- **原计划顺位 1–8 的源码开发与最终机器回归已完成**：Compile exit 0；全量 EditMode `224 total / 189 passed / 0 failed / 35 historical Explicit skipped`；全量 PlayMode `44 total / 38 passed / 0 failed / 6 visual-capture Explicit skipped` 且 Unity exit 0；包含 W13/W18 Composite Preview 的 Windows Player Build `1/1 passed`；Git ignore 与残留审计通过。视觉签署只剩 W2 一项暂时搁置，故仍不得关闭“全计划视觉完成”。

### 2.1 文档清单（按编号）

| 序号 | 文档 | 工作包 | 性质 | 产出规模 |
|---|---|---|---|---:|
| 00 | 本文档 | — | 索引 | — |
| 01 | `01_STYLE_SYSTEM.md` | W1 风格体系与共享资产底座 | 基础设施 | 8 种风格定义 + Style Pack 协议 |
| 02 | `02_UI_UX_DESIGN.md` | W2 VFX Studio 编辑器界面 | 基础设施 + UI | 1 个主窗口 5 个页签 |
| 03 | `03_LEGACY_VISUAL_CLOSURE.md` | W0 遗留视觉验收闭环 | 收尾 | 4 项文档回写 + Slash 定稿 |
| 04 | `04_FIRE_FAMILY.md` | W3 火焰族扩展 | 内容 | 8 个特效 |
| 05 | `05_FROST_FAMILY.md` | W4 冰霜族扩展 | 内容 | 7 个特效 |
| 06 | `06_LIGHTNING_FAMILY.md` | W5 雷电族扩展 | 内容 | 7 个特效 |
| 07 | `07_WATER_WIND_FAMILY.md` | W6 水系与风系 | 内容 | 8 个特效 |
| 08 | `08_EARTH_NATURE_TOXIC_FAMILY.md` | W7 岩土/自然/毒系 | 内容 | 8 个特效 |
| 09 | `09_HOLY_SHADOW_ARCANE_FAMILY.md` | W8 圣光/暗影/奥术 | 内容 | 9 个特效 |
| 10 | `10_2D_STYLE_SPECIALS.md` | W9 2D 风格专项（像素/手绘帧动画/水墨） | 内容 + 风格 | 9 个特效 |
| 11 | `11_3D_STYLE_SPECIALS.md` | W10 3D 风格专项（半写实/全息科幻/暗黑仪式） | 内容 + 风格 | 9 个特效 |
| 12 | `12_ENVIRONMENT_WEATHER.md` | W11 环境与天气扩展 | 内容 | 7 个特效 |
| 13 | `13_HIT_FEEDBACK_INTERACTION.md` | W12 打击感与交互连携 | 内容 | 7 个特效 |
| 14 | `14_COMPOSITE_ULTIMATE.md` | W13 大招/Boss 演出组合库 | 内容 | 6 个组合 |
| 15 | `15_SCREEN_UI_PACK.md` | W14 屏幕/UI 特效包 | 内容 | 6 个特效 |
| 16 | `16_NEW_ARCHETYPES.md` | W15 新增 6 个 Archetype（贴花/武器拖尾/破坏/死亡重生/传送/掉落） | 类型扩容 + 内容 | 6 类规则 + 10 个特效 |
| 17 | `17_STYLE_PACK_2.md` | W16 风格包第二批（低多边形/宝石/糖果/星空/蒸汽/幽魂） | 风格扩容 | 6 种风格 + 12 个打样 |
| 18 | `18_GAME_UI_INTERACTION_FX.md` | W17 游戏内交互 UI 特效包（按钮/卡牌/宝箱/抽卡/奖励飞行） | 内容 + UI | 10 个特效 |
| 19 | `19_CHARACTER_THEME_KITS.md` | W18 角色主题套装（4 套全技能链综合包） | 综合内容 | 4 套 ≈12 个新组件 |
| 20 | `20_CAPABILITY_MATRIX.md` | W-C0 能力矩阵总纲（behavior 块、素体规范、采样验收） | **能力基础设施** | 协议 + 验收机制 |
| 21 | `21_PROJECTILE_CAPABILITIES.md` | W-C1 弹道能力（直线/加速/抛物线/追踪/蛇形/回旋/弹跳/环绕突进/贯穿/分裂/跳跃/发射模式） | **能力** | 12 个素体 |
| 22 | `22_BEAM_RAY_CAPABILITIES.md` | W-C2 射线能力（瞬发/持续/扫射/蓄力/反射/遮挡/汇聚/链式 + 端点协议） | **能力** | 8 个素体 |
| 23 | `23_TIMING_AREA_CAPABILITIES.md` | W-C3 时序与范围能力（预警/延爆/Tick/蓄力释放/引导打断/连锁/扩张/聚爆/移动区域/阶段成长 + 视觉槽机制） | **能力** | 10 个素体 |

总规模：**30 个能力素体** + 约 **129 个成品特效族**（成品自能力批次后以"能力+皮肤"方式生产；含变体可交付条目约 250+）；风格 8+6=**14 种**；Archetype 14+6=**20 类**；能力 token **30+**；UI 覆盖编辑器工具（W2）、战斗屏幕（W14）、游戏交互（W17）三层。

---

## 3. 全局约定（所有批次共用，各文档不再重复）

### 3.1 命名与目录

- 特效 id：`<名称>_<archetype 简写>_<2d|3d>`，snake_case，与现有 `fireball_2d`、`frost_impact_2d` 一致。
- Recipe 放 `project/Assets/VFX/Recipes/<类别>/<id>.default.json`；风格变体为 `<id>.<style>.json`。
- Generated 输出 `project/Assets/VFX/Generated/<id>/`；共享资产进 `project/Assets/VFX/Shared/<元素或风格>/`，共享资产只计一次，禁止每个特效独占大 PNG（沿用九宫格经验）。
- 外部生成的原始图统一进 `ArtSource/VFX/<族>/RawGenerated → Modules → AtlasLayout`，流程按 `docs/rules/25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md`。

### 3.2 每个特效的规格字段（各批次文档中的"规格卡"格式）

每个特效在批次文档里必须给全以下字段，开发时照卡施工：

- **id / Archetype / 维度 / 生命周期**（one-shot、sustained、event-driven）
- **风格与配色**：风格 token（见 01 文档）+ 主/辅/点缀三色
- **视觉分层**：沿用九宫格 V2 五职责 —— 主体、内部高光、外部能量、次级粒子、消散；每层写清载体（Mesh/Quad/ParticleSystem/Trail）
- **Phase 时间线**：关键时刻点与该时刻必须可见的状态（同 S15 的验收帧表方式）
- **Recipe 可调参数**：本特效暴露给文字/Patch 修改的语义参数（强度、颜色、规模、速度、密度等，每个给范围）
- **静态预算**：峰值粒子 / ParticleSystem 数 / 材质数 / 透明 Renderer 数，默认继承 `mobile_medium`，超出必须在规格卡里声明理由
- **共享资产**：复用哪些、新增哪些（新增要进 Shared，不进 Generated）

### 3.3 每个批次的统一验收门槛（DoD）

1. `tools/compile-check.bat` 通过；EditMode/PlayMode 全绿。
2. 每个特效有 `<id>.default.json` Recipe，Schema 校验通过，重复构建结构幂等。
3. 每个特效在**批次预览场景**（每批次一个 3×N 宫格 Preview Scene，复用九宫格机制）中以唯一审查相机播放；3D 特效至少一个斜视角截图证明深度。
4. one-shot 完整播放并清空；sustained 稳定循环 ≥3 个周期；Stop/Reset 干净。
5. 静态预算预检通过，机器检查不以 `IsAlive`/像素存在代替视觉质量；人工验收看形状、层级、动作、消散。
6. 证据归档到 `docs/vfx-reviews/<批次名>/`（BRIEF / PRODUCTION_REPORT / evidence），被拒绝轮次按 `70_ITERATION_EVIDENCE_AND_LEARNING.md` 记录并回写 `60_ENGINEERING_LESSONS.md`。
7. 每个特效至少 1 个风格变体 Recipe 或 1 个语义 Patch 示例，证明可被文字驱动修改。

### 3.4 边界（继续不做）

运行时 AI、云端资产生成、其他引擎、真机性能认证、自动视觉评分，本轮仍然不做。所有 AI 参与仍然只产 Recipe/Patch JSON，经审核编译器落地。
