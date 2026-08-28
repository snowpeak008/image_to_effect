# W17 / W18 独立 next candidate 源码报告

> 日期：2026-08-25  
> 当前状态：`NEXT_CANDIDATE_VISUAL_PENDING`  
> 用户视觉结论：`null`  
> 机器状态：隔离 Unity Build r2 exit 0；最终定向 Edit/Preview/Play 为 `4/4 + 2/2 + 7/7`，共享回归为 `5/5 + 6/6 + 5/5 + 5/5`；独立终审 `GO`（P0=0、P1=0）  
> 边界：机器 GO 不构成视觉通过。主项目 GUI 未被控制；旧 Prefab、旧 Scene、旧证据和旧拒绝最终均保持原字节，也未触碰 S0a/S0b/S3 正式证据。

## 1. 交付结果

### W17 游戏 UI

- 10 份 `w17-ui-next-candidate/v1` Recipe，id 全部带 `_next_candidate`。
- 独立 `W17UiInteractionController`，全部表现由真实 `RectTransform/Graphic` 载体完成；不复用旧 `PlannedContentVfxController`，也不把枚举、标签或自报状态当作视觉。
- 按钮：真实圆角边缘行走、波纹/星屑/放射线；同一入口接受 `92×44 / 140×70 / 220×92` 三尺寸。
- 卡牌/宝箱/抽卡：真实 x 轴翻面、2–3 来源卡位移、宝箱盖/漏光/奖励预闪、五档 rarity 拓扑、十卡 2×5 落位与最高稀有度压轴。
- 奖励飞行：固定 12 槽池，起终点、弧高、错峰驱动真实二次贝塞尔位移；Stop/Reset 不产生新对象或残留。
- 进度条：`fill_ratio` 改变真实填充宽度、流光速度与满档端点脉冲。
- 每个 Entry 内置 `RectMask2D`；普通项 ≤24 Graphic，gacha ≤48，ParticleSystem=0。

### W18 角色主题

- 4 份 `w18-theme-next-candidate/v1` Recipe，对应炎刃武士、冰月法师、机械猎手、幽咒巫女。
- 独立 `W18CharacterThemeController`，每套以 palette 引用 + 形状语言 + Mesh/Line 拓扑共同形成主题，不做单纯换色：
  - 炎刃：锐角新月与上扬刀线；
  - 冰月：六边晶体与月轮；
  - 机械：齿轮与直线瞄准；
  - 幽咒：墨带、八符纸单 draw carrier 与十二鬼影队列。
- 同一 8 秒链路驱动 Idle→普攻链→位移→技能→大招→受击→死亡→登场；受击/死亡/登场改变实际 Renderer/Line/Shader 参数。
- 手、武器、胸口、脚底载体可重绑到外部 rig；Immediate Stop/Reset 恢复原父级、清空 Line、关闭 Renderer。
- 单套预算上限 Renderer 14、Material 3、ParticleSystem 1/容量 16；源码计划实际为 10–12 Renderer、1 Material、0 ParticleSystem。
- Preview 使用 `VFXComposer/NextCandidate/WorldCellClip` 的片元 `clip`；四套同屏各有不重叠世界裁剪矩形。

## 2. Build 与 Preview 入口

- 全部：`VFXComposer.Editor.NextCandidates.W17W18NextCandidateAuthoring.BuildAllForBatch`
- W17：`VFXComposer.Editor.NextCandidates.W17W18NextCandidateAuthoring.BuildW17ForBatch`
- W18：`VFXComposer.Editor.NextCandidates.W17W18NextCandidateAuthoring.BuildW18ForBatch`
- W17 Preview：`Assets/VFX/Preview/VFXPREVIEW_GameUI_NextCandidate.unity`（4×3，10 条目 + 2 个按钮尺寸复用格）。
- W18 Preview：`Assets/VFX/Preview/VFXPREVIEW_HeroKits_NextCandidate.unity`（2×2 四套同屏）。
- 状态根只记录候选归属：`W17_NEXT_CANDIDATE_VISUAL_PENDING` / `W18_NEXT_CANDIDATE_VISUAL_PENDING`；它们不参与效果可见性测试。
- 生成目录只允许 `Assets/VFX/Generated/W17W18NextCandidate/`；authoring 在构建前后哈希保护旧 W17/W18 Prefab 与旧 Preview Scene。

## 3. 定向门禁

隔离 Unity 预期过滤器与数量：

| 层 | Filter | 预期 |
|---|---|---:|
| EditMode | `VFXComposer.Tests.EditMode.W17W18NextCandidateEditModeTests` | 4/4 |
| Preview EditMode | `VFXComposer.Tests.EditMode.W17W18NextCandidatePreviewTests` | 2/2 |
| PlayMode | `VFXComposer.Tests.PlayMode.W17W18NextCandidateRuntimeTests` | 7/7 |

共享回归至少包括：

- EditMode：`VFXComposer.Tests.EditMode.W11W17IndependentContentTests`、`VFXComposer.Tests.EditMode.W13W18CompositeAndHeroKitTests`。
- PlayMode：`VFXComposer.Tests.PlayMode.W11W17IndependentRuntimeTests`、`VFXComposer.Tests.PlayMode.W13W18CompositeRuntimeTests`。

主 Goal 已在隔离 shadow 运行以上门禁。最终证据为 `edit-r2.xml`、`preview-r2.xml`、`play-r3.xml`；`play-r2.xml` 的 `6/7` 是固定等待错过短暂 clean-gap 的负面时序证据，已由使用有界状态轮询、仍严格观察 `InCleanGap → 全部 idle → replay alive` 的 r3 取代，不计入最终通过数。

共享回归最终为：旧 W11/W17 Edit `5/5`、旧 W13/W18 Edit `6/6`、旧 W11/W17 Play `5/5`、旧 W13/W18 Play `5/5`。回归后被旧 builder 重写的 `VFXPREVIEW_HeroKits.unity` 及 `.meta` 已从 canonical 精确恢复并复核哈希一致。

## 4. 已完成静态审计

- 使用 Unity 2022.3 实际 Bee response references 对新增 Runtime、Editor、Edit tests、Play tests 四程序集分别作 Roslyn 编译：`PASS / PASS / PASS / PASS`。
- 14/14 JSON 可解析。
- 候选源码资产 23 个，缺失 `.meta` 为 0；候选 `.meta` 28 个，GUID 28/28 唯一。
- `Assets/` + `Packages/` 共审计 1622 个 `.meta`，重复 GUID 组为 0（排除 Unity `Library/PackageCache` 的多版本缓存副本）。
- Canonical 尚未晋升 Generated/Preview 输出；隔离 shadow 已生成并核验 14 Prefab、14 Manifest、2 Preview Scene。14/14 Manifest 均为 `w17-w18-next-candidate-2`，buildHash 重算一致。
- ghost production Prefab 的 hand/weapon/chest/feet 载体为 `1/1/2/1`，`InkMissile` 真实归入 weapon；全 14 production Prefab 已逐项验证 AllowTail→idle→再次 Play→Immediate，并验证 Preview 自然 clean-gap→replay。
- 独立终审剩余两项 P2：增量 buildHash 尚未覆盖 Prefab/共享材质/Mesh 的完整依赖闭包；未来源码或依赖变化仍必须提升 compilerVersion。此次已从 `-1` 正确提升为 `-2`。

## 5. Exact project sync list

以下 51 个 Project 路径是从 canonical source 同步到 shadow 的完整清单；不得把未来 canonical `Generated/` 或 Preview 输出列入同步源：

```text
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate.meta
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17UiInteractionController.cs
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17UiInteractionController.cs.meta
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W18CharacterThemeController.cs
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W18CharacterThemeController.cs.meta
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs.meta
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17W18WorldCellClip.shader
project/Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17W18WorldCellClip.shader.meta
project/Packages/com.vfxcomposer.unity/Editor/NextCandidates.meta
project/Packages/com.vfxcomposer.unity/Editor/NextCandidates/W17W18NextCandidatePlan.cs
project/Packages/com.vfxcomposer.unity/Editor/NextCandidates/W17W18NextCandidatePlan.cs.meta
project/Packages/com.vfxcomposer.unity/Editor/NextCandidates/W17W18NextCandidateAuthoring.cs
project/Packages/com.vfxcomposer.unity/Editor/NextCandidates/W17W18NextCandidateAuthoring.cs.meta
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W17W18NextCandidateEditModeTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W17W18NextCandidateEditModeTests.cs.meta
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W17W18NextCandidatePreviewTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W17W18NextCandidatePreviewTests.cs.meta
project/Packages/com.vfxcomposer.unity/Tests/PlayMode/W17W18NextCandidateRuntimeTests.cs
project/Packages/com.vfxcomposer.unity/Tests/PlayMode/W17W18NextCandidateRuntimeTests.cs.meta
project/Assets/VFX/Recipes/W17W18NextCandidate.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/button_press_fx_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/button_press_fx_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/button_confirm_burst_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/button_confirm_burst_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/card_flip_reveal_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/card_flip_reveal_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/card_merge_fx_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/card_merge_fx_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/chest_open_burst_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/chest_open_burst_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/gacha_single_reveal_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/gacha_single_reveal_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/gacha_ten_sequence_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/gacha_ten_sequence_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/reward_fly_collect_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/reward_fly_collect_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/daily_check_stamp_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/daily_check_stamp_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/progress_charge_fx_ui_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W17/progress_charge_fx_ui_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W18.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/flame_blade_samurai_kit_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/flame_blade_samurai_kit_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/ice_moon_mage_kit_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/ice_moon_mage_kit_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/mechanical_hunter_kit_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/mechanical_hunter_kit_next_candidate.default.json.meta
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/ghost_curse_shrine_kit_next_candidate.default.json
project/Assets/VFX/Recipes/W17W18NextCandidate/W18/ghost_curse_shrine_kit_next_candidate.default.json.meta
```

## 6. 视觉边界

旧 W17/W18 拒绝原文与旧资产仍有效。新候选的 Runtime 状态、预算、硬裁剪、事件和清理可以由机器证明；形状层级、动作质感、主题一致性与商用品质只能由用户在最终阶段签署。未签署前不得写 `accepted`、`passed visual` 或“商用就绪”。
