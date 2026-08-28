# W8 圣光 / 暗影 / 奥术扩展（9 个特效）

> 实现状态（2026-08-25）：9/9 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：三大魔法系元素族。圣光核心是**垂直性与秩序**（光柱、对称、缓和缓出）；暗影核心是**吞噬与不规则**（负空间、边缘撕扯）；奥术核心是**几何符文与魔力流**。
> 批次预览场景：`VFXPREVIEW_MagicFamily.unity`（3×3 正好一屏）。
> 配色：圣 `#FFE9A8`/`#FFFFFF`；暗 `#5A2E8C`/`#1A0F2E`/点缀 `#C24DFF`；奥 `#4D7CFF`/`#9AD1FF`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| divine_smite_impact_3d | Impact | 3D | one-shot | stylized | 天罚光柱砸落+羽光飞散 |
| holy_halo_aura_2d | Aura | 2D | sustained | stylized | 头顶光环+环身光尘 |
| resurrection_spawn_3d | Spawn | 3D | one-shot | stylized | 复活光柱：光门开启+光羽上升 |
| shadow_claw_slash_2d | Slash | 2D | one-shot | dark | 三道爪痕撕裂+暗雾渗出 |
| void_orb_projectile_3d | Projectile | 3D | event | dark | 吞噬光线的虚空球，边缘扭曲 |
| shadow_grasp_area_2d | Area | 2D | sustained | dark | 地面暗池，鬼手周期抓握 |
| curse_mark_status_2d | Aura(状态) | 2D | sustained | dark | 诅咒印记：符文明灭+紫烟 |
| arcane_missile_projectile_2d | Projectile | 2D | event | stylized | 三连发追踪魔弹（错峰发射） |
| arcane_rune_spawn_2d | Spawn | 2D | one-shot | stylized | 双层反转符文阵展开 |

## 2. 规格卡

### divine_smite_impact_3d
- 分层：主体=天顶垂落光柱（上宽下聚，Reveal 从上而下 0.1s 内完成）；高光=柱芯过曝+落点十字闪；外部能量=落点金环外扩+竖直光羽 6–10 片升起；次级=光尘悬浮；消散=光柱上收（从下往上抽离），金辉残留 0.5s。
- 参数：pillar_height、pillar_radius、feather_count、afterglow。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤4 / Renderer ≤7；斜视角验证柱体圆截面。

### holy_halo_aura_2d
- 分层：主体=头顶椭圆光环（缓旋+呼吸明暗）；高光=环上游走亮点；外部能量=身周上升光尘；次级=偶发小十字闪 star sparkle；消散=光环上升淡出。
- 参数：halo_tilt、dust_density、sparkle_rate。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤3 / Renderer ≤5。

### resurrection_spawn_3d
- 分层：主体=地面光门圆阵展开+中央光柱升起；高光=门纹亮线扫描一周；外部能量=柱内光羽螺旋上升；次级=外围跪拜式光带 4 片缓起（对称）；消散=实体出现时点光柱化作光雨坠落。
- 参数：gate_radius、column_height、feather_spiral_speed。
- 预算：粒子 ≤64 / PS ≤4 / 材质 ≤5 / Renderer ≤7。

### shadow_claw_slash_2d
- 分层：主体=三道平行爪痕（依次 0.03s 错开撕开，边缘撕扯锯齿）；高光=痕芯亮紫；外部能量=痕口渗出暗雾下坠；次级=暗紫碎屑；消散=爪痕如伤口闭合（宽度收窄至消失）。
- dark：低明度背景色调压制，点缀色只给痕芯。
- 参数：claw_count 2–4、tear_jaggedness、mist_amount。
- 预算：粒子 ≤40 / PS ≤2 / 材质 ≤4 / Renderer ≤7。

### void_orb_projectile_3d
- 分层：主体=纯黑球核（无光照实心）+外缘引力扭曲环（UV 径向内吸流动）；高光=边缘细紫环（事件视界感）；外部能量=周围光点被吸入轨迹（粒子向心螺旋）；次级=尾部暗雾丝；消散=命中时内爆（先收缩 20% 再紫黑爆开）。
- 参数：orb_radius、suction_particle_rate、implode_scale。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### shadow_grasp_area_2d
- 分层：主体=地面暗池（边缘不规则蠕动）；高光=池面紫纹流动；外部能量=每 Tick 伸出 2–3 只鬼手抓握再缩回（Reveal 生长）；次级=池缘暗火苗；消散=池体向中心吞缩。
- 参数：pool_radius、hand_count、tick_interval、hand_height。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### curse_mark_status_2d
- 分层：主体=角色胸前/头顶符文印记（明灭呼吸，缺省紫）；高光=符文描边扫光；外部能量=印记渗出细紫烟；次级=偶发暗紫电弧绕体一周；消散=解除时符文碎裂为灰烬。
- 参数：mark_glyph 1–4（共享符文图集）、pulse_rate、smoke_amount。
- 预算：粒子 ≤24 / PS ≤2 / 材质 ≤3 / Renderer ≤5。

### arcane_missile_projectile_2d
- 分层：主体=菱形魔弹×3（0.1s 错峰、微蛇形航迹）；高光=弹芯白；外部能量=每弹细拖尾；次级=发射口小魔法阵闪现；消散=各自命中蓝紫小爆+符文碎片。
- 参数：missile_count 1–5、stagger_interval、wobble_amp。
- 预算：粒子峰值 ≤56 / PS ≤4 / 材质 ≤4 / Renderer ≤7。

### arcane_rune_spawn_2d
- 分层：主体=双层符文环（内外反向旋转）展开（缩放+旋转同步 Reveal）；高光=符文逐个点亮（8–12 个，顺序确定性）；外部能量=环间魔力弧连接；次级=中心魔力聚点脉冲；消散=符文逆序熄灭、环收拢。
- 参数：ring_radius、glyph_count、spin_speed、activate_order（顺/逆/随机种子）。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤4 / Renderer ≤7。

## 3. 批次验收
通用 DoD + 附加：圣光三效果对称性/垂直性人工检查；void_orb 的"吸入"粒子运动方向机器可验（向心速度分量）；符文类共享 `Shared/Textures/` 符文图集，禁止每效果新图。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将本项统一签署为：**拒绝，无法商用**。本 Scene 未作逐格/逐帧视觉核对，不据此伪造具体圣光/暗影/奥术画面问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. W8 全新 next-candidate 追加（2026-08-25）

本节是后续授权的新路径追加；旧候选、旧拒绝原文和旧证据边界保持不变。新候选版本为 `element-next-w6-w8-1`，输出根为 `Assets/VFX/NextCandidates/W6W8Elements/`，新 Scene 为 `VFXPREVIEW_HolyShadowArcaneFamily_NextCandidate.unity`，状态根为 `W8_NEXT_CANDIDATE_VISUAL_PENDING`。

- 圣光执行垂直与秩序：天罚在 0.1 归一化时间内自上而下 Reveal，再依次出现十字/金环/羽光，最后向上抽离并按 `afterglow` 留辉；光环以 `halo_tilt` 改椭圆几何，以密度/频率驱动光尘与十字闪；复活门、中央柱、对称光带和羽毛螺旋是分离载体。
- 暗影执行负空间与吞噬：爪痕按 0.03s 级错峰撕开，锯齿度改变折线，暗雾下坠并以宽度闭合；虚空球使用近黑实心核、事件视界边、确定性向心螺旋粒子及命中内爆；暗池不规则蠕动，2–3 只手按 tick Reveal 伸缩；诅咒 glyph、呼吸率和烟量都进入实体运行语义。
- 奥术执行离散几何秩序：1–5 枚魔弹按 `stagger_interval` 启动独立波动轨迹和命中；双层符文环反转旋转，8–12 glyph 按 forward/reverse/seeded_random 的确定性排列点亮，并在退出时逆向熄灭/收拢。
- Shadow body 使用 alpha 混合的暗核/边缘而非紫色 additive 团；Arcane glyph 使用离散网格/激活边，Holy 使用垂直 band。25 个 W6–W8 profile 都有独立 shape token、程序化 Mesh 与参数绑定，仍受固定 cell、Renderer/粒子/材质预算和池化 Reset 约束。

未来隔离入口：`BuildW8ForBatch`；单元素族入口为 `BuildHolyForBatch`、`BuildShadowForBatch`、`BuildArcaneForBatch`。定向 EditMode/PlayMode 类过滤器为 `VFXComposer.Tests.EditMode.W6W8ElementNextCandidateTests` 与 `VFXComposer.Tests.PlayMode.W6W8ElementNextCandidateRuntimeTests`。本次没有 Unity 执行、机器 XML 或用户视觉验收，新候选保持 `VISUAL_PENDING`。
