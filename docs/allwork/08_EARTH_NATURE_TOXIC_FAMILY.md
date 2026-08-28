# W7 岩土 / 自然 / 毒系扩展（8 个特效）

> 实现状态（2026-08-25）：8/8 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：三个相邻小族合批。岩土核心是**重量感**（顿挫时间曲线、尘土滞留）；自然核心是**生长动画**（藤蔓/花按 Reveal 生长而非淡入）；毒系在既有 toxic_field_3d 之外补 2D 与投射。
> 批次预览场景：`VFXPREVIEW_EarthNatureFamily.unity`。
> 配色：岩土主 `#A88860` / 尘 `#C9B48E`；自然主 `#5FBF5A` / 花 `#FFD1E8`；毒主 `#8CD62E` / 深 `#4E7A1E`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| earth_spike_spawn_3d | Spawn | 3D | one-shot | stylized | 岩刺列破土推进（可指向） |
| boulder_projectile_3d | Projectile | 3D | event | stylized | 翻滚巨石，落地碎裂扬尘 |
| quake_stomp_impact_3d | Impact | 3D | one-shot | stylized | 震地踏击：裂纹+浮石+尘墙 |
| thorn_snare_area_2d | Area | 2D | sustained | stylized | 荆棘缠绕区域，边缘藤刺生长 |
| vine_whip_slash_2d | Slash | 2D | one-shot | cartoon | 藤鞭抽击，鞭体波动+叶屑 |
| healing_bloom_aura_2d | Aura | 2D | sustained | cartoon | 治疗花环：花瓣+上升光叶 |
| spore_burst_impact_2d | Impact | 2D | one-shot | stylized | 毒孢囊爆裂，孢子云滞留 |
| acid_lob_projectile_2d | Projectile | 2D | event | cartoon | 抛物线酸液弹，落地腐蚀泡 |

## 2. 规格卡

### earth_spike_spawn_3d
- 分层：主体=5–8 块楔形岩刺沿直线依次破土（每块 2 帧急升+3% 过冲回落，重量感）；高光=断面亮边；外部能量=每块破土掀起土块+尘团；次级=细石弹跳；消散=岩刺整体下沉，尘土滞留 0.6s。
- 参数：spike_count、advance_speed、line_length 2–6m、rock_tint。
- 预算：粒子 ≤64 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

### boulder_projectile_3d
- 分层：主体=多面体巨石（低模、翻滚角速度与位移匹配）；高光=棱面受光闪；外部能量=尾部掉落碎石与尘迹；次级=落点碎裂 5–8 块飞散；消散=尘墙隆起后缓沉（尘比石头活得久，重量感关键）。
- 参数：boulder_scale、spin、impact_debris_count、dust_lifetime。
- 预算：粒子 ≤64 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### quake_stomp_impact_3d
- 分层：主体=放射地裂纹（Decal 面片依次点亮 4–6 条）；高光=裂纹内岩浆色微光（可参数关闭为纯土系）；外部能量=环形尘墙外扩；次级=浮石 4–6 块升起悬停再坠落；消散=裂纹合拢淡出。
- 时间线：`.0` 踏击白闪 → `.06` 裂纹全部展开（快）→ `.1–.4` 尘墙+浮石 → `.9` 清空。
- 参数：crack_count、radius、float_rock_count、magma_glow 0–1。
- 预算：粒子 ≤72 / PS ≤4 / 材质 ≤5 / Renderer ≤7。

### thorn_snare_area_2d
- 分层：主体=区域边界荆棘圈（藤条 Reveal 生长，非淡入）；高光=刺尖亮点；外部能量=区域内地面藤纹脉动；次级=飘落叶屑；消散=藤条枯萎变褐再缩回。
- 参数：radius、thorn_density、pulse_interval、wither_time。
- 预算：粒子 ≤40 / PS ≤2 / 材质 ≤4 / Renderer ≤6。

### vine_whip_slash_2d
- 分层：主体=藤鞭抽击弧（鞭体正弦波动传递到梢，末端最快）；高光=鞭梢速度线；外部能量=抽击点小爆闪+叶屑放射；次级=沿鞭飘叶；消散=鞭体回抽退场。
- cartoon：粗描边、鞭体 squash & stretch。
- 参数：whip_length、wave_amp、leaf_count。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤3 / Renderer ≤6。

### healing_bloom_aura_2d
- 分层：主体=脚底花环（4–6 朵花按序开放，Reveal 生长）；高光=花心闪光；外部能量=上升光叶与光尘；次级=环身缓旋光点；消散=花瓣合拢+光尘上散。
- 参数：flower_count、rise_speed、palette（花色可换证明 set_palette）。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### spore_burst_impact_2d
- 分层：主体=孢囊鼓胀 2 次后爆裂；高光=爆点酸绿闪；外部能量=孢子云团外扩后**滞留缓沉**（毒系特征：残留久）；次级=大孢子颗粒漂浮明灭；消散=云团边缘先散、中心后散，总残留 1.2s。
- 参数：cloud_radius、linger_time、spore_count。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤3 / Renderer ≤5。

### acid_lob_projectile_2d
- 分层：主体=酸液团（果冻形变、抛物线由外部驱动）；高光=团面高光滑动；外部能量=飞行滴落小酸珠；次级=落地酸泊+冒泡 3–5 处；消散=酸泊边缘蚀刻状收缩。
- 参数：blob_scale、drip_rate、pool_lifetime、bubble_rate。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤3 / Renderer ≤5。

## 3. 批次验收
通用 DoD + 附加：岩土三效果的"重量感"人工检查项（急起缓尘、顿挫曲线）；自然两效果的"生长而非淡入"检查项；spore_burst 残留时长机器可验（readback 活跃数曲线单调收敛）。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将本项统一签署为：**拒绝，无法商用**。本 Scene 未作逐格/逐帧视觉核对，不据此伪造具体岩土/自然/毒画面问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. W7 全新 next-candidate 追加（2026-08-25）

本节只追加后续授权的新候选，绝不替换上面的旧拒绝或证据。新候选使用 `element-next-w6-w8-1` 与 `Assets/VFX/NextCandidates/W6W8Elements/`，新 Scene 为 `VFXPREVIEW_EarthNatureToxicFamily_NextCandidate.unity`，状态根为 `W7_NEXT_CANDIDATE_VISUAL_PENDING`。

- 岩土以几何和顿挫表达重量：岩刺是 5–8 楔形断层按 `advance_speed/line_length` 依次急升、3% 过冲回落并留尘；巨石使用低模多面体滚转、命中碎块和更长尘尾；震地把 `crack_count/radius/magma_glow` 送入放射裂纹、冲击盘和矿物能量，浮石数量进入有界粒子批。
- 自然使用 Reveal 而非透明度假生长：荆棘圈按密度拓扑展开并按 tick 脉动，停止 tail 变褐、收缩；藤鞭的正弦波沿长度传播到梢后回抽，叶片数控制细节批；治疗花按 `flower_count` 顺序开放，`rise_speed` 驱动上升光叶。
- 毒系有自己的黏滞语法：孢囊双脉冲后进入单调收敛的滞留云；酸液弹有果冻团、滴率、命中酸泊、泡率和按 `pool_lifetime` 收蚀的残留，不借用自然/水体换色。
- 每项 content 参数都有 `parameter -> carrier/timing` 绑定；程序化 wedge/fault、thorn、sine-vine、botanical bloom、spore cloud、viscous blob/pool Mesh 均归属对应 next-candidate 输出。预算固定有界、单 PS、无 Rigidbody、确定性 seed，池化 Reset 清空 readback、arc 和粒子。

未来隔离入口：`BuildW7ForBatch`；单元素族入口为 `BuildEarthForBatch`、`BuildNatureForBatch`、`BuildToxicForBatch`。EditMode/PlayMode 过滤器为 `VFXComposer.Tests.EditMode.W6W8ElementNextCandidateTests` / `VFXComposer.Tests.PlayMode.W6W8ElementNextCandidateRuntimeTests`。当前未启动 Unity、未生成机器结果或视觉签署，状态仅为 `VISUAL_PENDING`。
