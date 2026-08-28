# W3–W8 六大元素族源码与机器验收报告

> 日期：2026-08-25  
> 状态：源码与机器门禁通过；W3–W8 用户视觉结论均为**拒绝**  
> 范围：W3 火焰、W4 冰霜、W5 雷电、W6 水/风、W7 岩土/自然/毒、W8 圣光/暗影/奥术

## 1. 交付结果

- 47 份默认 Recipe，覆盖计划表全部 id；另有 4 份风格变体。
- 每个默认 Recipe 均有一条 `set_content_param` 裸数组示例，共 47 条。
- 51 个 strict Runtime Entry（47 默认 + 4 变体），共享 Style Shader/Material/Mesh，不复制局部 PNG。
- 6 个正式批次 Preview Scene：Fire 8、Frost 7、Lightning 7、Water/Wind 8、Earth/Nature/Toxic 8、Magic 9。
- Recipe 1.3 增加可选 `content` 块；C# `ContentParameterRegistry` 是实时类型、枚举和范围权威，AI-readable Schema 与之同步。
- `StyledVfxController` 同时承载已注册 behavior 采样与 content 运行时只读协议；Preview replay driver 被项目规则禁止进入生产 Runtime Entry。

## 2. 所有权与构建

每个输出只有一个 `IVfxRuntimeEntry`；Manifest `enforcement=strict`，`ownedOutputs[]` 仅拥有自己的 Prefab，Style 共享资源进入 `dependencies[]`。本批没有成品独占纹理，因此 `localTextureBytes=0`。同一 Recipe 二次构建均为 `Unchanged`。

生成目录：

- `project/Assets/VFX/Recipes/Elements/`
- `project/Assets/VFX/Recipes/Patches/`
- `project/Assets/VFX/Generated/<effect-id>/`
- `project/Assets/VFX/Preview/VFXPREVIEW_*Family.unity`

## 3. 机器证据

- Compile：exit 0。
- 元素定向 EditMode：4/4，`test-results/w3-w8-elements-edit-v5.xml`。
- 元素定向 PlayMode：3/3，`test-results/w3-w8-elements-play-v3.xml`。
- 全量 EditMode：206 total / 171 passed / 0 failed / 35 historical Explicit skipped，`test-results/w3-w8-full-edit.xml`。
- 全量 PlayMode：30 total / 24 passed / 0 failed / 6 Explicit skipped，`test-results/w3-w8-full-play.xml`。

覆盖项包括：47 个 id/参数注册、非法 family/缺字段/未知字段/范围、47 次幂等构建、47 条稳定 Patch、Manifest/依赖所有权、6 个 Preview 的格子/相机/driver 隔离、11 个元素族运行协议、持续型三周期、停止/池化重置，以及 homing/parabola/dash/expand_ring 确定性采样。

定向运行测试发现并修复了 `dash` 仅登记而未实现 distance/duration 语义的问题；现在由纯逻辑 sampler 产生确定性缓动突进，Styled Runtime 复用同一轨迹。

## 4. 用户视觉签署（2026-08-25）

用户原话：

> 拒绝，无法商用，后续的特效类，都不验收了，这是通病了，都是同样的拒绝，无法商用，

| 工作包 | Scene | 结论与证据边界 |
|---|---|---|
| W3 火 | `VFXPREVIEW_FireFamily.unity` | **拒绝**。用户已实际检查并指出逻辑清晰但表现、颜色系统和逻辑表达均奇怪，整体无法商用。技术核对确认 47 个元素 Recipe 的 content 参数主要作为可读协议保存，当前通用 Controller 没有把大量成品参数消费为专用视觉；固定 Ring/Quad/Ribbon/Burst/Cone/Shard 与 Additive 调色造成同质化 |
| W4 冰 | `VFXPREVIEW_FrostFamily.unity` | **拒绝（批量签署）**。用户决定后续特效类不再逐 Scene 验收；未作逐格/逐帧画面核对 |
| W5 雷 | `VFXPREVIEW_LightningFamily.unity` | **拒绝（批量签署）**。用户决定后续特效类不再逐 Scene 验收；未作逐格/逐帧画面核对 |
| W6 水/风 | `VFXPREVIEW_WaterWindFamily.unity` | **拒绝（批量签署）**。用户决定后续特效类不再逐 Scene 验收；未作逐格/逐帧画面核对 |
| W7 岩土/自然/毒 | `VFXPREVIEW_EarthNatureFamily.unity` | **拒绝（批量签署）**。用户决定后续特效类不再逐 Scene 验收；未作逐格/逐帧画面核对 |
| W8 圣光/暗影/奥术 | `VFXPREVIEW_MagicFamily.unity` | **拒绝（批量签署）**。用户决定后续特效类不再逐 Scene 验收；未作逐格/逐帧画面核对 |

“无法商用”只记录为用户对视觉制作完成度的评价，不作版权、许可或法律解释。六个当前候选均记为 `rejected`；批量签署不伪造成用户已看过未打开的 Scene，也不为其补造格子或时间点问题。本次签署未授权重做、修改源码/资产或生成下一候选。

## 5. W3–W5 后续 next-candidate 实现追加（2026-08-25）

本节记录后续独立实现任务，不修改或替代上面的旧候选、旧拒绝结论与证据边界。W3–W5 新候选使用独立编译器版本 `element-next-w3-w5-1`、独立输出根 `Assets/VFX/NextCandidates/W3W5Elements/` 和独立共享根 `Assets/VFX/Shared/ElementNextCandidate/`；不会覆盖 `Assets/VFX/Generated/<effect-id>/`、原三族 Preview Scene、原 Recipe/Patch 或 W-C1/W-C2/W-C3、W1、W15、W6+ 输出。

| 工作包 | 新 Preview Scene | 状态根 | 新执行语义 |
|---|---|---|---|
| W3 火 | `VFXPREVIEW_FireFamily_NextCandidate.unity` | `W3_NEXT_CANDIDATE_VISUAL_PENDING` | 燃烧点火、喷发、余烬、热浪、焦痕/余焰 tail 分层；火环、喷火锥、凤凰、连爆与护盾均有独立载体及时序 |
| W4 冰 | `VFXPREVIEW_FrostFamily_NextCandidate.unity` | `W4_NEXT_CANDIDATE_VISUAL_PENDING` | 结晶生长、棱面锐度、霜雾、碎裂与融解/下沉分离；冰刺排布、晶瓣、冰封上升和碎片数量由内容参数驱动 |
| W5 雷 | `VFXPREVIEW_LightningFamily_NextCandidate.unity` | `W5_NEXT_CANDIDATE_VISUAL_PENDING` | 确定性折线分叉、离散跳闪、蓄电、瞬发放电、受击反弧与冲击余辉；不以平滑曲线或连续噪声冒充闪电 |

22 个 W3–W5 默认 Recipe 的全部 content 参数在纯计划层拥有显式 `parameter -> carrier/timing` 绑定；数值/布尔参数由新 Runtime executor 每帧读取，影响实体载体的宽高、半径、层数、粒子数、分叉数、离散步进或生命周期，`pattern` 等拓扑参数进入逐效果程序化 Mesh，`fuel_color` 与 `hit_flash_color` 直接进入相应 Renderer 的属性块。每个效果的主载体 Mesh 保存在自己的 next-candidate 输出目录，不再复用旧固定 body Mesh。Body 与 atmosphere 使用 alpha blend，仅瞬时高光/电芯使用 additive，避免“共享 Mesh + Additive 换色”成为三族视觉语法。

Runtime 仍保持一个 `IVfxRuntimeEntry`、无 Rigidbody、确定性 seed、固定粒子缓冲、池化 `ResetForPool`、Immediate/AllowTail 双停止路径。每个 Prefab 上限为 7 个透明 Renderer、1 个 ParticleSystem、120 粒子、3 个实际共享材质；Preview 使用固定 3×3 cell、独立 effect/label bounds 和按编译 extent 计算的缩放。候选 manifest/preview receipt 明确写入 `visualStatus=VISUAL_PENDING`、`userVisualVerdict=null`、`oldRejectedCandidateModified=false`、`machineEvidenceIsVisualAcceptance=false`。

雷盾 `walk_arc_count` 可请求 0–8；7 Renderer 总预算下最多同时保留 5 个池化 arc carrier，6–8 层以确定性离散轮换映射到这 5 个 carrier，并让请求层数继续影响 cadence、采样拓扑和运行时 multiplicity 读回。该压缩是明确预算协议，不伪装成 8 条同时存在的 Renderer。

新增 EditMode/PlayMode/Preview 门禁源码覆盖：22 项参数绑定、三族 Patch 到实体 carrier、W3–W5-only 过滤、旧/其他工作包路径不变、逐效果 Mesh、混合模式、预算、幂等构建、格子/标签/跨格边界、火/冰/雷阶段语义、雷电确定性、清理与复播。当前仅完成 Bee/Roslyn 静态编译校验（Runtime 全量、new Editor、EditMode 与 PlayMode 新门禁均 exit 0），本次未启动 Unity、未生成机器测试 XML、未进行用户视觉签署；因此三个新候选仍是 `VISUAL_PENDING`，不得记为 accepted、L3 或 L4。

## 6. W6–W8 后续 next-candidate 实现追加（2026-08-25）

本节继续同一独立框架，但使用追加版本 `element-next-w6-w8-1` 与独立根 `Assets/VFX/NextCandidates/W6W8Elements/`；W3–W5 的 `element-next-w3-w5-1`、22 个 profile、输出路径、Scene 和状态根保持原样。旧正式输出 `Assets/VFX/Generated/`、旧三份 W6–W8 Scene、旧 Recipe/Patch、旧拒绝与证据均不是新 compiler 的 owned target。

| 工作包 | 新 Preview Scene | 状态根 | 专属视觉语义 |
|---|---|---|---|
| W6 水/风 | `VFXPREVIEW_WaterWindFamily_NextCandidate.unity` | `W6_NEXT_CANDIDATE_VISUAL_PENDING` | 水体积/拉丝/泡沫/飞溅/水渍/停止下垂；风低透明介质、尘叶雪、细弧、流线和残影 |
| W7 岩土/自然/毒 | `VFXPREVIEW_EarthNatureToxicFamily_NextCandidate.unity` | `W7_NEXT_CANDIDATE_VISUAL_PENDING` | 岩土重量/急升过冲/裂纹/长尘；自然 Reveal 生长/脉动/开花/枯萎回抽；毒黏滞双脉冲/滞留收敛/腐蚀酸泊 |
| W8 圣/暗/奥术 | `VFXPREVIEW_HolyShadowArcaneFamily_NextCandidate.unity` | `W8_NEXT_CANDIDATE_VISUAL_PENDING` | 圣光垂直有序 Reveal/十字/羽光；暗影负空间/错峰撕裂/向心吸入/内爆/鬼手；奥术错峰魔弹/确定性符文激活与反向关闭 |

25 个新增 profile 的全部 content 参数都在 plan 中拥有显式 carrier/timing 绑定，并在 Runtime 中改变长度、半径、厚度、介质类型、程序化拓扑、层数、粒子数、事件时序、Reveal/linger/tail 或确定性激活顺序。新增 strand jet、curl wall、bubble shell、crown、vortex/funnel、wedge fault、thorn、sine vine、botanical bloom、viscous blob/pool、ordered pillar/gate、claw tear、solid void、grasp hand、curse glyph、missile fan 与 rune ring 等程序化 Mesh；不是旧 fixed Mesh 或单一 additive 材质换色。共享 Shader 保留 Fire/Frost/Lightning 原分支，并为 Water/Wind/Earth/Nature/Toxic/Holy/Shadow/Arcane 增加互不相同的流纹、低透明介质、矿物棱面、生长叶脉、黏滞胞状、有序垂直、暗核事件视界和离散 glyph 语言。

Runtime 继续使用固定数组、确定性 hash、最多 5 条池化 arc、1 个 ParticleSystem、最多 120 粒子、最多 7 Renderer 和 3 个实际共享材质；每个 profile 进一步应用权威预算。Reset/Immediate/AllowTail 清空 carrier、arc、粒子与 W6–W8 读回。Preview 复用 W3–W5 的固定 3×3 cell、effect/label 分区与 extent 缩放协议；receipt/manifest 固定为 `VISUAL_PENDING`、`userVisualVerdict=null`、`oldRejectedCandidateModified=false`、`machineEvidenceIsVisualAcceptance=false`。

未来隔离 Unity 入口：

- 全批：`VFXComposer.Editor.Elements.ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch`
- 工作包单独：`BuildW6ForBatch`、`BuildW7ForBatch`、`BuildW8ForBatch`
- 元素族单独：`BuildWaterForBatch`、`BuildWindForBatch`、`BuildEarthForBatch`、`BuildNatureForBatch`、`BuildToxicForBatch`、`BuildHolyForBatch`、`BuildShadowForBatch`、`BuildArcaneForBatch`
- EditMode filter：`VFXComposer.Tests.EditMode.W6W8ElementNextCandidateTests`
- PlayMode filter：`VFXComposer.Tests.PlayMode.W6W8ElementNextCandidateRuntimeTests`
- 必要共享回归：`VFXComposer.Tests.EditMode.W3W5ElementNextCandidateTests`、`VFXComposer.Tests.PlayMode.W3W5ElementNextCandidateRuntimeTests`，并保留现有 W-C1/W-C2/W-C3、W1、W15 门禁。

本轮没有启动或关闭 Unity，没有生成 Scene/Prefab/manifest/测试 XML，也没有触碰 shadow/write-once evidence。当前验证边界仅是 Bee/Roslyn 静态编译；新增 EditMode/PlayMode/Preview 测试只是待未来隔离 Unity 执行的门禁源码，不能写成机器通过，更不能写成用户视觉通过。W6–W8 新候选仍为 `VISUAL_PENDING`。

## 7. 隔离 Unity 机器闭环（2026-08-25）

第 5、6 节末尾的“仅静态编译”是源码停笔时的历史边界；主 Goal 随后在隔离 shadow 中执行了真实 Build 与测试。独立只读终审结论为 machine/结构门 **GO**（P0=0、P1=0），可进入人工视觉复核；这不改变六个状态根的 `VISUAL_PENDING`，也不产生用户视觉签署。

- W3–W5：22 Recipe / 22 Prefab / 22 manifest / 3 Preview Scene+receipt；定向 Edit `5/5`、Play r2 `6/6`。
- W6–W8：25 Recipe / 25 Prefab / 25 manifest / 3 Preview Scene+receipt；定向 Edit `6/6`、Play r2 `8/8`。
- 共享回归：旧 Elements `4/4 + 3/3`、W-C1 `7/7 + 2/2`、Beam `5/5 + 9/9`、Timing `6/6 + 5/5`、W1 `3/3 + 4/4`、W15 `3/3 + 8/8`。
- 最终指定 16 份 XML 合计 `84/84`，failed/skipped/inconclusive 均为 0；18/18 XML 与全部可核日志均来自指定 shadow projectPath。
- 47 个 Recipe hash 与 manifest 一致；47 个 buildHash、carrier shape、topology signature 各自唯一。207 个 owned recipe-shaped Mesh、47 个 Runtime Entry 及 sidecar 完整；全项目 2304 个 Assets/Packages GUID 无重复。
- 六个 Preview 条目数为 `8/7/7/8/8/9`，各自只有 1 Camera、1 driver，receipt/Scene/status root 一致；预算峰值仍为 120 particles、7 transparent Renderers、3 Materials、1 ParticleSystem、0 local texture bytes。
- 100 个旧 Prefab/manifest/Scene 的额外污染扫描无新候选 GUID、executor/driver/cell GUID 或 next-candidate 路径；机器回归未把新路径写回旧候选。

首次 W6–W8 Play r1 的 `5/8` 被保留为负面证据：未触发事件的 sentinel 原为 `-1000`，导致 bubble 提前进入 residue、void suction 提前归零。`ElementNextCandidateVisualExecutor` 已把字段初值、`Play()` 和 `CompleteReset()` 全部改为 `float.PositiveInfinity`，只在真实事件发生时写入当前 elapsed；源码 SHA-256 为 `0CD82B8CD863AF142FB3D63498FAEBBA4BCEEB96AE813D878EC51391E86C91A0`。Unity 日志证明 r2 重新导入并编译该 Runtime 后通过 `8/8`，随后 W3–W5 r2 与全部共享回归继续全绿。

证据目录仍保留被 r2 取代的 r1 XML，以及七份许可证失效时未生成 XML 的旧日志；它们是 P2 证据卫生项，不属于最终通过集合，不得与上面的 16 份最终 XML 混算。
