# W3 火焰族扩展（8 个特效）

> 实现状态（2026-08-25）：8/8 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已拒绝当前视觉候选，结论为“拒绝，无法商用”。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：把火系从"火球 + 少量点缀"扩展为覆盖主要 Archetype 的完整元素族，并作为风格体系（W1）落地的第一个内容批次。
> 共享资产：`Shared/Fire/` 扩充火舌笔刷遮罩、火花图集、热浪噪声；全批次复用，禁止每效果独占。
> 批次预览场景：`VFXPREVIEW_FireFamily.unity`（3×3 宫格，第 9 格留空或放 fireball 对照）。
> 通用配色基准：主 `#FF6A00` / 辅 `#FFD84D` / 点缀 `#FFF6E0`；dark 变体主 `#C22E1F`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| flame_slash_2d | Slash | 2D | one-shot | stylized | 宽新月火焰刀光（S15 视觉目标的 2D 正式化） |
| fire_nova_burst_3d | Impact | 3D | one-shot | stylized | 以自身为中心的环形火焰冲击波 |
| flamethrower_beam_3d | Beam | 3D | sustained | semireal | 锥形持续喷火 |
| burning_status_aura_2d | Aura(状态) | 2D | sustained | cartoon | 角色身上的燃烧 debuff 挂点火苗 |
| ember_rain_area_3d | Area | 3D | sustained | stylized | 区域火雨与地面燃烧斑 |
| phoenix_dart_projectile_2d | Projectile | 2D | event | cartoon | 凤凰形投射物，翼形尾焰 |
| chain_blast_impact_2d | Impact | 2D | one-shot | stylized | 3 连锁定点爆破（延迟接力） |
| fire_shield_3d | Shield | 3D | event | stylized | 环身旋转火焰护盾，受击喷发 |

## 2. 规格卡

### flame_slash_2d
- 分层：主体=宽新月能量 Mesh（笔刷遮罩 Reveal）；高光=黄白内刃；外部能量=红橙碎焰舌 6–10 片；次级=4–8 颗菱形火星；消散=溶解燃边。
- 时间线：`.03` 左下点火楔 → `.10` 扫掠中段宽体 → `.166` 峰值（占屏宽 40–55%）→ `.23` 红色余焰 → `.45` 清空。（沿用 S15 验收帧表。）
- 参数：sweep_angle 60–140°、arc_width 0.3–1.0、spark_count 0–12、palette。
- 预算：粒子峰值 ≤40 / PS ≤3 / 材质 ≤4 / 透明 Renderer ≤7。
- 变体：`.inkwash`（墨焰）、`.neon`。

### fire_nova_burst_3d
- 分层：主体=地面扩张火环 Mesh；高光=中心闪光柱；外部能量=向外翻卷火舌带；次级=放射状火星 + 上升烟；消散=环外缘溶解、地面焦痕淡出。
- 时间线：`.05` 中心闪+隆起 → `.15` 火环达最大半径 80% → `.3` 火舌翻卷可见 → `.6` 焦痕残留 → `1.2` 清空。
- 参数：radius 1–6m、ring_speed、tongue_count 8–16、scorch_lifetime 0–3s。
- 预算：粒子 ≤64 / PS ≤4 / 材质 ≤5 / Renderer ≤7；斜视角必须看出环是贴地 3D 而非 Billboard。

### flamethrower_beam_3d
- 分层：主体=锥形喷射 Mesh（VfxSoftNoise 双层滚动）；高光=喷口白芯；外部能量=外围松散火团粒子；次级=飞溅火星、热浪扭曲（噪声 UV 偏移模拟，不用后处理）；消散=停火后 0.4s 余焰 tail。
- 生命周期：sustained，Start/Stop 事件驱动；喷口与目标端点可外部驱动（复用 Beam 端点协议）。
- 参数：length 2–8m、cone_angle 10–35°、intensity、fuel_color。
- 预算：粒子 ≤80 / PS ≤4 / 材质 ≤4 / Renderer ≤6。

### burning_status_aura_2d
- 分层：主体=2–3 簇挂点小火苗（帧感跳动）；高光=火苗芯；外部能量=上升热气丝；次级=间歇小火星；消散=熄灭时缩小+一缕烟。
- cartoon 风格：3 级色阶+描边，火苗轮廓 Q 弹（squash & stretch 曲线）。
- 参数：flame_count 1–4、tick_pulse（掉血节拍脉冲开关）、palette。
- 预算：粒子 ≤24 / PS ≤2 / 材质 ≤3 / Renderer ≤5；挂点协议同 Status 类（角色挂点、随载体移动）。

### ember_rain_area_3d
- 分层：主体=区域边界地环+落火流星；高光=落点小爆闪；外部能量=地面燃烧斑（随机 3–6 处）；次级=上升火星与烟；消散=停止后燃烧斑逐个熄灭。
- 参数：radius 2–8m、rain_density、tick_interval、burn_patch_count。
- 预算：粒子 ≤96 / PS ≤5 / 材质 ≤5 / Renderer ≤7（区域类放宽档，Manifest 声明）。

### phoenix_dart_projectile_2d
- 分层：主体=凤鸟剪影核心（双翼 Mesh 摆动）；高光=鸟身白芯；外部能量=翼形拖尾（TrailRenderer×2 分层）；次级=羽状火星；消散=命中化作放射羽焰。
- Launch/Travel/Impact 三阶段独立可播（沿用 Projectile 协议）。
- 参数：wing_span、trail_length、impact_feather_count、palette。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### chain_blast_impact_2d
- 分层：每爆点：主体=爆闪圆+冲击环；高光=白芯；外部能量=放射火舌；次级=碎屑火星；消散=烟圈。三点按 0.12s 接力，位置由参数数组给出。
- 参数：blast_count 2–4、interval 0.08–0.3s、per_blast_scale、spread_pattern（线形/三角）。
- 预算：粒子峰值 ≤72（错峰后实际更低）/ PS ≤4 / 材质 ≤4 / Renderer ≤7。

### fire_shield_3d
- 分层：主体=半透明球壳（Fresnel 暖色）；高光=壳面流动火纹（UV 流动）；外部能量=环绕 2 条火焰轨道带；次级=表面逸出火星；消散=解除时壳体上卷燃尽。
- 事件：OnHit → 命中点喷发火舌+壳体脉冲（复用 Shield 命中波协议）。
- 参数：shell_radius、orbit_speed、hit_burst_scale、palette。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤5 / Renderer ≤7。

## 3. 批次验收
按 `00_INDEX` 3.3 通用 DoD 执行；另加：
- flame_slash_2d 必须通过 S15 风格家族比对（宽新月、层级、碎边），并可作为 Slash 视觉遗留项（W0 移交）的正式关闭载体。
- 至少 2 个特效交付第二风格变体 Recipe；至少 1 个交付语义 Patch 示例（建议 ember_rain_area_3d 的 rain_density 减半）。

## 4. 用户视觉拒绝记录（2026-08-25）

用户已实际检查 `VFXPREVIEW_FireFamily.unity`，指出逻辑清晰但表现、颜色系统和逻辑表达均奇怪，并签署：**拒绝，无法商用**。技术核对确认大量 content 参数未形成专用视觉，固定共享 Mesh 与 Additive 调色造成同质化。本结论只评价视觉制作完成度，不作许可或法律解释；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。

## 5. 后续 W3 next-candidate（视觉待签署）

后续独立任务已建立新路径；本节不改写第 4 节旧拒绝。新 Scene 为 `Assets/VFX/Preview/VFXPREVIEW_FireFamily_NextCandidate.unity`，状态根必须保持 `W3_NEXT_CANDIDATE_VISUAL_PENDING`，运行输出位于 `Assets/VFX/NextCandidates/W3W5Elements/Generated/<id>/`，compiler version 为 `element-next-w3-w5-1`。

- 新 Runtime executor 按火系语法执行点火/燃烧、喷发、余烬、热浪、焦痕与 AllowTail 余焰；不是旧 `StyledVfxController` 的换色分支。
- 8 个效果各有自己的程序化主载体：燃烧新月、火舌环、分层火锥、多火苗簇、燃烧斑场、凤凰翼形、连爆花形与旋转火壳。Body/残留采用 alpha blend，additive 只用于内芯/爆闪。
- `sweep_angle/arc_width/spark_count`、火环 radius/speed/tongue/scorch、喷火 length/cone/intensity/`fuel_color`、燃烧 tick、火雨密度/节拍/斑块、凤凰翼展/拖尾/命中羽焰、连爆数量/间隔/尺度/排布、护盾半径/转速/受击喷发均进入实际几何、层数、粒子批或时序。`fuel_color` 直接覆盖喷火主体 Renderer 属性块。
- 固定上限为 7 Renderer、1 ParticleSystem、单 Prefab 粒子不超过 96（全局硬上限 120）；无 Rigidbody，使用确定性 seed、固定缓冲和池化 Reset。

新增门禁会检查燃烧→喷发→余烬/热浪→残留阶段、内容参数改变实体 carrier、Immediate/AllowTail 清理复播、预算、幂等构建、Preview effect/label/cell 边界及旧 W3/其他工作包输出不变。当前仅静态编译通过，未执行 Unity 测试或用户视觉检查；机器结果不得把本候选写成视觉通过。
