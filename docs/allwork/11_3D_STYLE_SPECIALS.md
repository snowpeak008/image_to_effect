# W10 3D 风格专项：半写实 / 全息科幻 / 暗黑仪式（9 个特效）

> 实现状态（2026-08-25）：9 个专项条目 + `prismatic_shield_3d.holo` 复用变体、双层噪声/确定性故障/暗色域协议与双机位 Preview 已完成；机器门禁通过，用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：沉淀三条 3D 风格管线（semireal / holo / dark），每条 3 个代表特效。同 W9，交付物一半是管线能力。
> 批次预览场景：`VFXPREVIEW_Style3D.unity`；所有效果双机位验收（正视+斜 45°）。

## 1. 三条风格管线

### P4 半写实链（semireal）
- 方案：`VfxSoftNoise` 多层滚动噪声 + 软粒子边缘 + HDR 强度分级（不开 Bloom 后处理，靠贴图内辉光带层次——沿用"无后处理依赖"原则）。烟/火/尘的密度-寿命曲线库进共享 Authoring。
- 关键：噪声细节必须双层异速（大形 0.3×、细节 1.7×），单层噪声即"塑料感"，列入人工检查项。

### P5 全息链（holo）
- 方案：`VfxHoloFresnel`（Fresnel 边缘光+扫描线+故障闪断）。故障闪断参数化：`glitch_rate`、`glitch_offset`（UV 撕裂幅度）、确定性种子。
- 色域约束：加色蓝青为主，允许单警示色（红/橙）。

### P6 暗黑仪式链（dark）
- 方案：符文图集 + 烟雾噪声 + 低明度色域强制（style 块校验 value ≤0.6，点缀色除外）。

## 2. 清单

| id | Archetype | 生命周期 | 管线 | 一句话 |
|---|---|---|---|---|
| real_explosion_impact_3d | Impact | one-shot | P4 | 半写实爆炸：火团-烟柱-冲击尘环 |
| smoke_plume_area_3d | Area | sustained | P4 | 持续浓烟柱（着火点烟源） |
| muzzle_flash_impact_3d | Impact | one-shot | P4 | 枪口焰：星形闪+烟+抛壳火星 |
| holo_barrier_shield_3d | Shield | event | P5 | 全息六边屏障，受击涟漪+故障 |
| holo_scan_area_3d | Area | sustained | P5 | 扫描光环扫过区域，标记点亮 |
| glitch_blink_transform_3d | Transform | one-shot | P5 | 数字化闪现：碎片化消失/重组 |
| blood_ritual_spawn_3d | Spawn | one-shot | P6 | 血色法阵+烛焰环+黑烟柱召唤 |
| soul_drain_beam_3d | Beam | sustained | P6 | 摄魂链接：灵魂碎片逆流 |
| demon_eruption_impact_3d | Impact | one-shot | P6 | 恶魔之手破地爆发+黑火 |

## 3. 规格卡

### real_explosion_impact_3d（P4 详卡）
- 分层：主体=火团 3–4 团错峰翻滚（双层噪声、由亮转暗渐变为烟）；高光=初帧过曝闪+局部亮芯；外部能量=地面冲击尘环横扫；次级=抛射火星拖尾 6–10 条+溅起碎屑；消散=烟柱上升变灰、缓慢撕开消散（烟寿命≥火 3 倍）。
- 时间线：`.0` 闪 → `.08` 火团峰值 → `.3` 火转烟 → `.6` 尘环停 → `1.8` 烟散尽。
- 参数：blast_scale、fireball_count、dust_ring、smoke_lifetime。
- 预算：粒子 ≤96（放宽档声明）/ PS ≤5 / 材质 ≤5 / Renderer ≤7。

### smoke_plume_area_3d — 烟源持续上升浓烟柱，风向弯曲参数、根部橙色火光可选；参数：plume_height、wind_bend、ember_glow；预算 PS ≤3。
### muzzle_flash_impact_3d — 1–2 帧星形枪口焰（3 面十字 Mesh）、口部烟、侧抛火星；总时长 ≤0.12s（速度感验收项）；参数：flash_scale、petal_count 4–6；预算 PS ≤3。

### holo_barrier_shield_3d（P5 详卡）
- 分层：主体=六边网格曲面屏障（Fresnel+扫描线上行）；高光=网格节点呼吸；外部能量=边缘轮廓光带；次级=偶发故障闪断（局部 UV 撕裂 2 帧，确定性种子）；消散=关闭时按行扫描熄灭（数字百叶窗）。
- 事件：OnHit → 命中点六边涟漪扩散 3 圈+该区短暂故障。
- 参数：hex_density、scan_speed、glitch_rate、barrier_shape（平面/弧面/球）。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤4 / Renderer ≤6。

### holo_scan_area_3d — 扫描光环从中心周期外扩，扫过处地面网格短暂点亮+随机 2–3 个标记柱升起；参数：scan_interval、mark_count；预算 PS ≤3。
### glitch_blink_transform_3d — 本体按体素块碎片化位移消失（0.2s）→ 目标位重组（逆序）；碎片确定性种子；参数：voxel_size、blink_distance 表现层、reassemble_time；预算 PS ≤3。

### blood_ritual_spawn_3d（P6 详卡）
- 分层：主体=血红法阵展开（双环+符文逐个点亮）+中央黑烟柱升起；高光=符文亮起扫序+烛焰 5 点环布；外部能量=阵面血液流动纹（UV 流向中心）；次级=上升黑烟丝与血珠悬浮；消散=召唤时点阵面碎裂、黑烟外炸。
- 参数：circle_radius、rune_set、candle_count、smoke_height。
- 预算：粒子 ≤64 / PS ≤4 / 材质 ≤5 / Renderer ≤7。

### soul_drain_beam_3d — 波浪暗紫链接束，灵魂碎片（小面片人形/磷火）沿束逆流向施法者，目标端渗出暗雾；参数：drain_rate、wisp_count、束下垂度；预算 PS ≤3。
### demon_eruption_impact_3d — 地面裂纹渗黑火 → 恶魔之手 Mesh 破土上抓（Reveal 生长）→ 黑火环爆+灰烬升腾；参数：hand_scale、black_fire_amount、ash_lifetime；预算 PS ≤4。

## 4. 批次验收
通用 DoD + 附加：P4 三效果过"双层异速噪声"人工检查；P5 故障闪断帧序列确定性机器可验；P6 色域校验（低明度强制）；每条管线出使用说明进 `docs/ai-workflow/`，并用 P5 链给 prismatic_shield_3d 出 `.holo` 变体作为复用证明。

## 5. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W10 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造半写实、全息或暗黑的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 6. 后续独立 next-candidate（2026-08-25）

用户后续已授权全项目继续开发；旧候选、旧 Scene 与上述拒绝记录继续保持不变。本轮另建 W10 源码 `next_candidate`：10 个条目以真实 Mesh/LineRenderer 执行 `Anticipation → MaterialHit → Sustain → Dissolve`，爆炸、烟柱、枪口焰、屏障命中、扫描标记、闪现重组、仪式点亮、摄魂逆流和破土上升分别有独立拓扑及时序观测点。未来隔离构建目标为 `VFXPREVIEW_Style3D_NextCandidate.unity`，当前只完成源码与静态门禁，状态为 `NEXT_CANDIDATE_VISUAL_PENDING`；没有新的用户视觉结论。详见 `docs/stage-notes/W9_W10_W16_NEXT_CANDIDATE_REPORT.md`。
