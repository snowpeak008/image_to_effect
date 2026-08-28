# W12 打击感与交互连携扩展（7 个特效）

> 实现状态（2026-08-25）：7/7 Recipe、strict Runtime Entry、语义 Patch与运行时协议已完成；MPB 受击闪白、格挡粒子碰撞、吸血动态端点束均有机器断言；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：补齐"战斗手感"层的特效：受击反馈、暴击、格挡、击飞、连击、元素反应、吸血链接。延续交互九宫格（interaction-gallery）已验证的动态端点驱动、阶段互斥等协议；该 gallery 被拒项如有移交（见 03 文档），并入本批次重做。
> 批次预览场景：`VFXPREVIEW_HitFeedback.unity`（含两个假人靶标与自动攻击循环驱动器，驱动器仅预览用）。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 一句话 |
|---|---|---|---|---|
| hit_flash_status_2d | Status | 2D | event | 受击白闪+边缘描红（材质闪帧，非 UI） |
| critical_strike_impact_2d | Impact | 2D | one-shot | 暴击：放射裂屏线+大字感星爆（无文字） |
| parry_spark_impact_3d | Impact | 3D | one-shot | 格挡火花：金属迸溅+短促闪环 |
| knockup_launcher_impact_3d | Impact | 3D | one-shot | 击飞：地面爆环+竖直气浪柱 |
| combo_surge_aura_2d | Aura(可叠层) | 2D | sustained | 连击气场：1–5 层强度递进 |
| elemental_reaction_burst_2d | Impact | 2D | one-shot | 元素反应爆发：双色能量对撞融合 |
| lifesteal_link_beam_2d | Beam | 2D | sustained | 吸血链接：红雾珠粒逆流回体 |

## 2. 规格卡

### hit_flash_status_2d
- 分层：主体=受击体材质白闪 2 帧（MPB 驱动，不换材质）；高光=轮廓描红 1 帧；外部能量=命中方向小迸点 3–5 颗；次级=无；消散=闪帧结束即净。
- 关键协议：作用于外部提供的目标 Renderer 集合（Status 挂点协议扩展"材质注入"通道，只写 MPB 不改材质资产）。
- 参数：flash_frames 1–3、tint（白/红/毒紫）、edge_width。
- 预算：粒子 ≤8 / PS ≤1 / 材质 ≤2 / Renderer ≤3（手感类必须极轻）。

### critical_strike_impact_2d
- 分层：主体=1 帧放射裂纹线（6–10 条不等长硬直线）+四角星爆闪；高光=中心过曝点；外部能量=倾斜冲击环单圈；次级=金色迸点上抛；消散=裂纹线 0.1s 内退缩。总时长 ≤0.35s。
- 参数：crack_count、star_scale、palette（金/红/紫档）。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤3 / Renderer ≤6。

### parry_spark_impact_3d
- 分层：主体=接触点扇形金属火花喷射（重力弧线+2 次弹跳）；高光=接触点 1 帧白闪+短横闪环；外部能量=小烟屑；次级=火花拖细尾；消散=火花落地熄灭。总时长 ≤0.5s。
- 参数：spark_count 8–16、cone_angle、bounce 0–2。
- 预算：粒子 ≤40 / PS ≤2 / 材质 ≤3 / Renderer ≤5。

### knockup_launcher_impact_3d
- 分层：主体=地面爆环+竖直气浪柱（半透明上冲，0.25s 内完成）；高光=柱内上升亮线 2–3 条；外部能量=外围尘环；次级=上抛碎屑随柱；消散=柱顶散开成环。
- 参数：column_height 2–4m、ring_scale、debris_count。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### combo_surge_aura_2d
- 分层：主体=环身气流带（层数越高圈数/速度越高）；高光=层级色阶（1–2 层白→3 层金→4–5 层红金）；外部能量=脚底冲击小环每升层脉冲一次；次级=上升粒点密度随层数；消散=断连时整体泄气下落。
- 关键协议：`set_stack_level 1–5` 运行时参数（Runtime 组件公开方法，Recipe 声明每层差异），是"可叠层状态"协议的定版载体。
- 参数：stack_level、per_level_palette、pulse_on_levelup。
- 预算：粒子 ≤56（5 层峰值）/ PS ≤3 / 材质 ≤4 / Renderer ≤7。

### elemental_reaction_burst_2d
- 分层：主体=双色能量团对撞（左右各一色迅速合体）→ 融合爆（第三色）；高光=融合帧白闪；外部能量=双色螺旋缠绕外环；次级=两色碎粒对冲飞散；消散=第三色烟圈。
- 关键协议：`color_a/color_b/result_color` 全参数化，一个 Recipe 覆盖火+冰=蒸、雷+水=感电等所有配对（配色由上层给）。
- 参数：三色、burst_scale、swirl_turns。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### lifesteal_link_beam_2d
- 分层：主体=下垂弧形红雾链接带（双端点动态驱动，沿用 channel_tether 协议）；高光=带内血珠粒子逆流（目标→施法者）；外部能量=目标端渗出红雾；次级=施法者端吸入闪点；消散=断链时带体从目标端向施法者端收缩。
- 参数：drain_rate（珠粒流速/密度）、sag（下垂度）、palette。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤3 / Renderer ≤6。

## 3. 批次验收
通用 DoD + 附加：全批次"手感时长"人工检查（反馈类峰值必须在前 20% 时间内到达）；hit_flash 的 MPB 注入通道不改材质资产哈希（机器验）；combo_surge 五层逐级演示录像；预览场景的自动攻击驱动器不进入任何正式 Prefab。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W12 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造打击峰值、方向、叠层或连携的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. 后续全项目授权下的独立 next candidate（2026-08-25）

上述拒绝原文与旧候选保持不变。用户其后另行授权继续全项目开发并把视觉验收统一延后到最后；据此新增 7 个 `w12nc_*`、独立 Recipe/Generated/Shared 根与 `VFXPREVIEW_W12_HIT_FEEDBACK_NEXT_CANDIDATE.unity`。新实现使用外部 Renderer MPB 原样恢复、真实碰撞火花、独立竖直击飞、1–5 真实 Ring、双色会聚后第三色释放和 20 点动态端点吸血束；当前仅完成源码和 Roslyn 静态编译，状态严格为 `NEXT_CANDIDATE_VISUAL_PENDING`。隔离 Unity build/Test Runner 与最终用户视觉签署尚未执行，详见 `../stage-notes/W11_W13_NEXT_CANDIDATE_REPORT.md`。
