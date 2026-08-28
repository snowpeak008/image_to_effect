# 全计划源码交付与最终视觉验收清单

> 日期：2026-08-24  
> 源码状态：**原计划顺位 1–8 已完成开发与机器门禁**  
> 视觉状态：**24/25 已由用户签署（4 项有条件通过、20 项拒绝）；1/25 暂时搁置且待签署（W2 Studio UI）；执行者未代签任何视觉结论**  
> 权威范围：`docs/allwork/00_INDEX_AND_ACCEPTANCE.md`

## 1. 最终机器结果

- Unity：2022.3.62f3c1，Compile exit 0。
- 全量 EditMode：224 total / 189 passed / 0 failed / 35 historical Explicit skipped。
- 全量 PlayMode：44 total / 38 passed / 0 failed / 6 visual-capture Explicit skipped；Unity exit 0。
- Windows Player Build：包含正式 Fireball、Slash 及 W13/W18 两个 Composite Preview，1/1 passed；外部临时构建已清理。
- Git ignore audit：passed。
- 资源残留审计：208 个正式 Generated 目录；0 个 temp/pending/backup 测试目录，0 个 Recipe history 残留；`git diff --check` passed。

历史 Explicit 测试是写证据/录图/一次性 AI cohort 测试，不属于普通产品回归；跳过不代表产品测试失败。

## 2. 打开方式

1. 用 Unity Hub/Editor 打开 `D:/WorkWork/Assist/image_to_smart/project`。
2. 在 Project 面板按下表双击 Scene，进入 Play。
3. 每个 Preview 至少看完一个完整循环；组合大招与角色套装建议看完所有自动切换条目。
4. 只记录“通过”、“有条件通过 + 条件/问题”或“拒绝 + 具体格子/时间点/原因”。机器结果不能替代你的画面结论。

## 3. 原计划视觉验收顺序

### A. W0 遗留闭环（`03_LEGACY_VISUAL_CLOSURE.md`）

| 项 | Scene | 核心判断 | 用户结论 |
|---|---|---|---|
| S15 Slash | `Assets/VFX/Preview/S12_SlashGeneratedPreview.unity` | 起点同步、弧面扫出、主刃/余焰/火星层级、收尾清空 | **有条件通过**（用户，2026-08-24）：“内容还可以，但无法做商用”；不等同于无条件视觉通过或商用认可 |
| 2D 九宫格 V2 | `Assets/VFX/Preview/VFXPREVIEW_ValidationGallery_3x3.unity` | 九格各自可读、时序完整、无静态占位 | **有条件通过**（用户，2026-08-24）：“这个我已经审核过了，还是看似通过，但是无法做商用级别”；不等同于无条件视觉通过或商用级认可 |
| 空间覆盖 B | `Assets/VFX/Preview/VFXPREVIEW_CoverageGalleryB_3x3.unity`，Screen/UI 另看 `VFXPREVIEW_DamageWarningUI_Fullscreen.unity` | 3D 空间感、Screen/UI 全屏布局、无遮挡/裁切 | **有条件通过**（用户，2026-08-25）：“这个是通过的，但还是无法商用”；不等同于无条件视觉通过或商用认可 |
| 交互九宫格 | `Assets/VFX/Preview/VFXPREVIEW_InteractionGallery_3x3.unity` | Homing/Dash/Channel/Chain 等真实交互与复播 | **有条件通过**（用户，2026-08-25）：“这个也看过了，内容可见，但不可商用”；不等同于无条件视觉通过或商用认可 |

### B. 顺位 1：能力批次

| 工作包 | Scene | 原计划判断 | 用户结论 |
|---|---|---|---|
| W-C1 弹道 | `VFXPREVIEW_CapProjectile.unity` | 12 种运动/命中/发射能力是否一眼可区分 | **旧候选拒绝保持有效**（用户，2026-08-25）：格 10–12 缺 Split 5 子弹、分时 Chainhop、扇形/错峰/环形 Volley。后续 W24 全项目开发授权下已另建下一候选并在隔离 Unity 通过 `7/7 + 1/1` EditMode、`2/2` PlayMode；新候选仍为 `VISUAL_PENDING`，尚未得到用户结论。 |
| W-C2 射线 | `VFXPREVIEW_CapBeam.unity` | hitscan、持续、扫射、蓄力、反射、遮挡、汇聚、跳线 | **旧候选拒绝保持有效**（用户，2026-08-25）：“拒绝；格1缺少衰减，格2缺少端点跟随，格3无扫射，格4仅变粗，格5无反射多段，格6无烧灼点和遮挡变化，格7无四源汇聚，格8无跳线。”后续 W24 授权下已另建下一候选，在隔离 Unity 通过 EditMode `5/5 + 1/1`、PlayMode `9/9`，并通过 W-C1 共享回归 `7/7 + 1/1 + 2/2`；新候选仍为 `VISUAL_PENDING`，尚未得到用户结论。 |
| W-C3 时序/范围 | `VFXPREVIEW_CapTiming.unity` | 预警、延爆、Tick、蓄力、引导、连锁、扩/缩环、移动区、成长 | **旧候选拒绝保持有效**（用户，2026-08-25）：“拒绝；W-C3仅完成逻辑采样，10格同时播放且跨格重叠、裁切严重；各格共用同色圆环，缺少预警爆发、加速闪烁、Tick视觉、三级蓄力、双出口、独立连锁爆点、聚爆、残留和升级脉冲等原计划视觉表达。”后续 W24 全项目开发授权下已另建 4×3 有界下一候选，在隔离 Unity 通过 EditMode `6/6`、PlayMode `5/5`、Preview `1/1`，并通过 W-C1 `7/7 + 2/2 + 1/1` 与 W-C2 `5/5 + 9/9 + 1/1` 共享回归；新候选仍为 `VISUAL_PENDING`，尚未得到用户结论。 |

行为正确性已有纯逻辑采样和事件契约门禁；这里仅判断表达是否清楚，不新增原计划外验收项。

### C. 顺位 3–4：风格底座、Studio、新 Archetype

| 工作包 | 入口 | 原计划判断 | 用户结论 |
|---|---|---|---|
| W1 风格底座 | `VFXPREVIEW_W1_StyleSamples.unity` | 8 种 style token 的视觉差异、共享底座，以及 fan+wave / charge+occlude / telegraph+nova 三份“能力+皮肤”示范 | **拒绝**（用户，2026-08-25），用户原话：“拒绝；W1样例尺寸未统一，Stylized基准过大、Dark frost过弱，Holy新星与Holo射线跨格、裁切并遮挡标签；场景缺少格内约束和8种style token的完整视觉对比，整体未达到商用级视觉完成度。”当前候选记为 `rejected`；“未达到商用级”仅指视觉完成度，不作许可或法律解释；该拒绝记录及旧 Scene 保持不变。其后用户另行授权的 W1-only `next_candidate` 已新增，目标 Scene 为 `VFXPREVIEW_W1_StyleSamples_NextCandidate.unity`；隔离机器门禁已通过 EditMode `3/3`、Preview `1/1`、PlayMode `4/4` 及定向共享回归，当前仍为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`，尚无新的用户视觉结论 |
| W2 Studio UI | 菜单 `Tools > VFX Composer > Studio` | Library/Composer/Inspector/Preview/Build Report 五页签和按能力浏览 | **暂时搁置；仍待签署**（用户，2026-08-25）；不折算为通过、拒绝或视觉完成 |
| W15 新 Archetype | `VFXPREVIEW_NewArchetypes.unity`<br>Next: `VFXPREVIEW_W15_NEXT_CANDIDATE.unity` | Decal、WeaponTrail、Destruction、LifeCycle、Portal、Loot 的空间/生命周期语义 | **拒绝**（用户，2026-08-25），用户原话：“拒绝；W15仅有六类Archetype的概念轮廓，Decal缺少三表面贴附，WeaponTrail缺少快慢挥差异，Destruction缺少完整破碎表现，LifeCycle未绑定角色溶解，Portal缺少出入口时序差异，Loot五档主要只换颜色；设计与实现不同步，整体未达到商用级视觉完成度。”逐类时间阶段、原计划预期、当前实际及技术核对见 `docs/stage-notes/W15_NEW_ARCHETYPES_REPORT.md` §5；旧候选、拒绝记录和 Scene 保持不变。授权后的 W15-only next candidate 已生成 10 Recipe/Prefab/manifest 与 1 Preview；定向 Edit/Preview/Play `3/3 + 1/1 + 8/8`、修复后共享 `6/6 + 2/2`，独立机器终审 GO（P0=0、P1=0），旧 W15 protected 11/11 exact。状态仍为 `W15_NEXT_CANDIDATE_VISUAL_PENDING`，尚无新的用户视觉结论 |

### D. 顺位 5：六大元素族

| 工作包 | Scene | 用户结论 |
|---|---|---|
| W3 火 | `VFXPREVIEW_FireFamily.unity`<br>Next: `VFXPREVIEW_FireFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25）：用户已检查本 Scene，认为“逻辑很清晰，但是表现很奇怪，颜色系统奇怪，逻辑表现也奇怪”，并签署“拒绝，无法商用”；技术核对确认内容参数大多未进入专用视觉、固定共享 Mesh 与 Additive 配色造成同质化。未授权重做。**后续 next-candidate**：独立输出与 executor 已实现；W3–W5 定向 `5/5 + 6/6`、全 W3–W8/共享最终 16 XML `84/84`，独立机器终审 GO（P0=0、P1=0）。状态根仍为 `W3_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null`，不得记为 accepted |
| W4 冰 | `VFXPREVIEW_FrostFamily.unity`<br>Next: `VFXPREVIEW_FrostFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25，批量签署）：用户决定后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐格/逐帧视觉核对，未授权重做。**后续 next-candidate**：独立结晶/霜雾/碎裂/融解 executor 已实现并通过同一 W3–W5 机器门及共享回归；状态根仍为 `W4_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null` |
| W5 雷 | `VFXPREVIEW_LightningFamily.unity`<br>Next: `VFXPREVIEW_LightningFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25，批量签署）：用户决定后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐格/逐帧视觉核对，未授权重做。**后续 next-candidate**：独立分叉/离散闪烁/蓄放电/余辉 executor 已实现并通过同一 W3–W5 机器门及共享回归；状态根仍为 `W5_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null` |
| W6 水/风 | `VFXPREVIEW_WaterWindFamily.unity`<br>Next: `VFXPREVIEW_WaterWindFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25，批量签署）：用户决定后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐格/逐帧视觉核对，未授权重做。**后续 next-candidate**：独立水体积/泡沫/飞溅/下垂与低透明风介质/碎屑/流线 executor 已实现；修复事件 sentinel 后 W6–W8 定向 `6/6 + 8/8` 并通过全部共享回归，独立机器终审 GO。状态根仍为 `W6_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null` |
| W7 岩土/自然/毒 | `VFXPREVIEW_EarthNatureFamily.unity`<br>Next: `VFXPREVIEW_EarthNatureToxicFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25，批量签署）：用户决定后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐格/逐帧视觉核对，未授权重做。**后续 next-candidate**：独立重量/过冲/尘、Reveal 生长/枯萎与黏滞/滞留/腐蚀 executor 已实现并通过同一 W6–W8 机器门及共享回归；状态根仍为 `W7_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null` |
| W8 圣光/暗影/奥术 | `VFXPREVIEW_MagicFamily.unity`<br>Next: `VFXPREVIEW_HolyShadowArcaneFamily_NextCandidate.unity` | **拒绝**（用户，2026-08-25，批量签署）：用户决定后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐格/逐帧视觉核对，未授权重做。**后续 next-candidate**：独立有序垂直圣光、负空间/吸入/内爆暗影及错峰/确定性符文奥术 executor 已实现并通过同一 W6–W8 机器门及共享回归；状态根仍为 `W8_NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null` |

逐格检查：能力行为未因套皮改变；主次层、元素辨识、循环/one-shot 时序、屏幕占比符合各批次规格卡。

### E. 顺位 6：独立内容线

| 工作包 | Scene | 原计划判断 | 用户结论 |
|---|---|---|---|
| W11 环境/天气 | `VFXPREVIEW_Environment.unity`<br>Next: `VFXPREVIEW_W11_ENVIRONMENT_NEXT_CANDIDATE.unity` | 场景氛围、近中远层、循环稳定 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对，当时未授权重做。授权后的 7 个 `w11nc_*` 独立候选已完成隔离 Build；全包定向 `4/4 + 1/1 + 7/7`、共享 `96/96`，旧/其他输出隔离通过。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`，尚待最终用户视觉结论 |
| W12 打击/连携 | `VFXPREVIEW_HitFeedback.unity`<br>Next: `VFXPREVIEW_W12_HIT_FEEDBACK_NEXT_CANDIDATE.unity` | 峰值前置、命中方向、叠层与连携 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对，当时未授权重做。授权后的 7 个 `w12nc_*` 独立候选已通过同一全包机器门与共享回归；可恢复 MPB、真实碰撞/竖直载体、1–5 环叠层、双色会聚与动态端点逆流均由 production Prefab 门禁覆盖。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING` |
| W14 Screen/UI | `VFXPREVIEW_ScreenUI.unity` | 16:9/19.5:9、安全区、屏幕语义 | **拒绝**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对，未授权重做 |
| W17 游戏交互 UI | `VFXPREVIEW_GameUI.unity`<br>Next: `VFXPREVIEW_GameUI_NextCandidate.unity` | 按钮、卡牌、宝箱、抽卡、奖励飞行的界面锚定 | **拒绝**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对，未授权重做。其后 W24 全项目授权下已新增隔离 W17 源码候选：10 份真实 Canvas 载体、3 尺寸按钮、rarity/skip/fill/merge/reward≤12 固定池与 `RectMask2D` 硬裁剪；隔离 Unity Build r2、定向 Edit/Preview/Play `4/4 + 2/2 + 7/7` 及共享回归 `5/5 + 6/6 + 5/5 + 5/5` 已通过，独立机器终审 GO（P0=0、P1=0）。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`、`userVisualVerdict=null`，尚待最终用户视觉结论 |

### F. 顺位 7：风格专项

| 工作包 | Scene | 原计划判断 | 用户结论 |
|---|---|---|---|
| W9 2D 风格专项 | `VFXPREVIEW_Style2D.unity`<br>Next: `VFXPREVIEW_Style2D_NextCandidate.unity` | 像素、手绘帧动画、水墨的离散时序与轮廓 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对。W24 授权下的独立 next candidate 已完成；W9/W10/W16 共 32 Recipe/Prefab/manifest 与 3 Preview，定向 `4/4 + 1/1 + 4/4`、共享 `67/67`、合计 `76/76`，独立机器终审 GO（P0=0、P1=0）。旧资产未覆盖，当前仍为 `NEXT_CANDIDATE_VISUAL_PENDING` |
| W10 3D 风格专项 | `VFXPREVIEW_Style3D.unity`<br>Next: `VFXPREVIEW_Style3D_NextCandidate.unity` | 半写实、全息科幻、暗黑仪式；正视与斜视空间感 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对。独立 3D next candidate 已通过上述 76/76 机器门与旧资产隔离；LegacyRuntime label/header、真实 Material 与空间载体已核验。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`，尚待最终用户视觉结论 |
| W16 风格包二 | `VFXPREVIEW_StylePack2.unity`<br>Next: `VFXPREVIEW_StylePack2_NextCandidate.unity` | 低多边形、宝石、糖果、星空、蒸汽、幽魂 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐条/逐帧视觉核对。六组 A/B next candidate 已通过上述 76/76 机器门；67 个生产 meta GUID 唯一且无旧 GUID 引用。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`，尚待最终用户视觉结论 |

### G. 顺位 8：综合编排

| 工作包 | Scene | 原计划判断 | 用户结论 |
|---|---|---|---|
| W13 大招/Boss | `VFXPREVIEW_Ultimate.unity`<br>Next: `VFXPREVIEW_W13_ULTIMATE_NEXT_CANDIDATE.unity` | 6 套分阶段演出、camera hint、Boss gate、峰值转场 | **旧候选拒绝保持有效**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐套/逐帧视觉核对，当时未授权重做。六个 `w13nc_*` 已用 W13-only 入口在 canonical 17 依赖 overlay 上重建；6/6 buildHash、31/31 path/GUID/fileID、205 个保护文件 0 变化与四项方法级 `1/1` 均通过，独立 canonical-overlay 终审 GO（P0=0、P1=0）。Preview Scene 跨进程 fileID 非字节幂等保留为 P2；状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`，30 文件晋升尚未执行 |
| W18 角色套装 | `VFXPREVIEW_HeroKits.unity`<br>Next: `VFXPREVIEW_HeroKits_NextCandidate.unity` | 4 套 palette/形状语言一致性、技能链节奏、切换后无视觉残留 | **拒绝**（用户，2026-08-25，批量签署）：后续特效类不再逐 Scene 验收，统一结论为“同样的拒绝，无法商用”；本项未作逐套/逐帧视觉核对，未授权重做。其后 W24 全项目授权下已新增隔离 W18 源码候选：4 套独立 palette/形状 Mesh+Line、完整技能阶段、外部 rig 挂点、预算硬上限和 shader 世界矩形裁剪；ghost 的 hand/weapon/chest/feet 四槽与全 14 production Prefab 双退出、Preview clean-gap/replay 已由机器验证，隔离 Unity 定向与共享门禁全部通过并获独立机器 GO。状态仍为 `NEXT_CANDIDATE_VISUAL_PENDING`、`userVisualVerdict=null`，尚待最终用户视觉结论 |

## 4. 签署规则

- 通过：在本表把“待签署”改为“通过”，写日期与签署人。
- 有条件通过：原样记录条件/问题、日期与签署人；不折算为无条件通过，不据此声明商用就绪，也不自动授权重做。
- 拒绝：写明 Scene、条目/格子、时间点、预期与实际，并把当前候选记为 `rejected`；只有获得用户明确授权后，才按 `docs/rules/70_ITERATION_EVIDENCE_AND_LEARNING.md` 进入重做。
- W0 四项全部有结论后，再回写 `docs/allwork/03_LEGACY_VISUAL_CLOSURE.md` 的关闭记录。
- 所有表格均由用户签署后，才可把“全计划视觉完成”写入总索引。
