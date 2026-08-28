# W13 大招 / Boss 演出组合库（6 个组合）

> 开发状态（2026-08-25）：**源码、严格构建与机器门禁完成；用户已通过后续特效类批量签署拒绝当前视觉候选。** 已交付 6 个 dependency-reference-only Composite Runtime Entry、时间轴/相机提示/阶段门协议和 `VFXPREVIEW_Ultimate.unity`；未授权重做。详见 `../stage-notes/W13_W18_COMPOSITE_HERO_KITS_REPORT.md`。

> 目标：在 ultimate_sequence_3d 的 Composite 协议基础上，产出 6 套多阶段大招演出。Composite 只做**编排**：引用既有族的子特效（前序批次产物）+少量专属主体，时间轴由 Recipe 声明，禁止把子特效复制进 Composite 目录（引用共享，构建期校验依赖递归报告——沿用九宫格资源所有权规则）。
> 因此本批次排在所有元素/风格批次之后开发。
> 批次预览场景：`VFXPREVIEW_Ultimate.unity`（每次只演一套，下拉切换）。

## 1. 清单

| id | 时长 | 阶段结构 | 主要引用 |
|---|---|---|---|
| dragon_breath_ultimate_3d | ~4s | 蓄力→龙首成形→扫射吐息→余燃 | flamethrower_beam_3d、ember_rain_area_3d、fire_nova_burst_3d |
| meteor_shower_ultimate_3d | ~5s | 天空预兆→连续陨石 5–8 颗→尘幕收场 | meteor_impact_3d、smoke_plume_area_3d、warning_telegraph_3d |
| frozen_domain_ultimate_3d | ~6s | 冰环外扩→领域冰封→全场碎裂 | blizzard_area_3d、flash_freeze_transform_3d、ice_spike_spawn_3d |
| judgement_ray_ultimate_3d | ~4s | 符文阵→聚能→天罚巨柱→灰烬羽落 | arcane_rune_spawn_2d(3D 适配)、focus_charge_3d、divine_smite_impact_3d |
| demon_gate_boss_3d | ~8s | 血阵→黑烟门→恶魔手破门→威吓咆哮波 | blood_ritual_spawn_3d、demon_eruption_impact_3d、rift_spawn_3d |
| blade_tempest_ultimate_3d | ~5s | 拔刀蓄势→环身刀风 8 连斩→收刀定格闪 | slash_3d_stylized(变体×8 编排)、gale 流线、parry_spark_impact_3d |

## 2. Composite 协议扩展（本批次基础设施项，先做）

在现有 Composite 语义上补三项，写入 Manifest 与 Schema：

1. **时间轴事件**：`timeline: [{t, ref_id, action(play/stop), overrides}]`，overrides 仅允许该子 Recipe 声明可外调的语义参数（如 palette、scale）。
2. **镜头提示通道**（仅数据，不实现相机）：`camera_hints: [{t, type(shake/zoom/slowmo), strength}]`，供接入方消费；预览场景提供最小消费器演示。
3. **阶段门**：`gates: [{t, wait_for(external_event)}]`，Boss 演出可在阶段间等待外部事件（如动画帧事件），预览用自动放行驱动器。

## 3. 编排规格要点（每套开发时展开为完整时间轴表）

- 每套组合的规格 = 时间轴表（每 0.1s 粒度列出 play/stop/overrides）+ 峰值预算合并表（同屏活跃子特效预算求和，声明 Composite 放宽档：粒子 ≤200 / PS ≤10 / 材质 ≤10 / Renderer ≤14）+ 三个验收帧（每阶段峰值各一）。
- 蓄力阶段统一复用 focus_charge_3d 参数变体，不新做蓄力特效。
- blade_tempest 的 8 连斩使用同一 slash 子 Recipe 的 8 个 transform/palette overrides 实例，验证"一个子 Recipe 多实例编排"。
- demon_gate 的阶段门用两个 `wait_for` 演示（门成形后等待、恶魔手出现前等待）。

## 4. 批次验收
通用 DoD + 附加：依赖递归报告零复制资产；每套一镜到底录像（authority 路径）；子特效单独回归（被引用不改其原验收哈希）；camera_hints 最小消费器演示 shake/slowmo 各一次。

机器状态：6 条 W13/W18 EditMode 与 5 条 PlayMode 定向测试通过；视觉录像/画面结论不由执行者代签，统一留到最终用户验收。

## 5. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W13 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐套/逐帧视觉核对，不据此伪造六套大招的节奏、构图、镜头或阶段问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 6. 后续全项目授权下的独立 next candidate（2026-08-25）

上述拒绝原文与旧候选保持不变。用户其后另行授权继续全项目开发并把视觉验收统一延后到最后；据此新增 6 个 `w13nc_*`、独立 Recipe/Generated/Shared 根与 `VFXPREVIEW_W13_ULTIMATE_NEXT_CANDIDATE.unity`。每个新 Prefab 只有四个本地阶段根和只读子 Prefab 引用；运行时固定池与重播姿态还原、play/stop timeline、三类 camera hint 的 Scene-only 消费器、Demon Gate 双 named gate、Meteor 六实例与 Blade 八同源 Slash 均有专用协议。当前仅完成源码和 Roslyn 静态编译，状态严格为 `NEXT_CANDIDATE_VISUAL_PENDING`。隔离 Unity build/Test Runner 与最终用户视觉签署尚未执行，详见 `../stage-notes/W11_W13_NEXT_CANDIDATE_REPORT.md`。
