# W9 2D 风格专项：像素 / 手绘帧动画 / 水墨（9 个特效）

> 实现状态（2026-08-25）：9 个专项条目 + `fireball_2d.pixel` 复用变体、三条 Style Contract、共享最小图集与正式 Preview 已完成；机器门禁通过，用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：验证并沉淀三条 2D 风格管线，每条 3 个代表特效。与元素批次不同，本批次的**交付物一半是管线**：像素量化链、帧动画图集链、水墨笔刷链，后续任何特效可复用这三条链产出对应风格变体。
> 批次预览场景：`VFXPREVIEW_Style2D.unity`。

## 1. 三条风格管线（先建链，后做效果）

### P1 像素链（pixel）
- 方案：正常粒子/Mesh 渲染 → `VfxPixelQuantize` 全效果量化（目标 64–128 虚拟分辨率）+ 调色板 LUT + 时间量化（12fps 采样，动画"跳帧"感）。
- Manifest 声明：pixel 风格强制 `snap_fps`、`palette_lut`、`virtual_res` 三参数；禁用平滑 Alpha 渐变（量化为 4 级）。

### P2 手绘帧动画链（cartoon-anime）
- 方案：关键形状用少帧数帧动画图集（6–10 帧，如爆炸烟团经典 smear 序列），配程序粒子补充碎片。图集走 `25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md`：RawGenerated → 清洗 → AtlasLayout 登记。
- Manifest 声明：`atlas_id`、`fps`、`loop_mode`；帧动画层与粒子层职责在 Recipe 中分离。

### P3 水墨链（inkwash）
- 方案：`VfxInkBrush` + 笔刷/晕染遮罩；色域约束（墨阶 4 级 + 单点缀色）由 style 块 `palette` 强制校验。
- Manifest 声明：`ink_density`、`bleed_radius`、`flyaway_threshold`（飞白）。

## 2. 清单

| id | Archetype | 生命周期 | 管线 | 一句话 |
|---|---|---|---|---|
| pixel_burst_impact_2d | Impact | one-shot | P1 | 经典像素爆炸：白闪→橙团→烟圈三段 |
| pixel_sword_slash_2d | Slash | one-shot | P1 | 3 帧像素刀光+击中星形迸点 |
| pixel_heal_aura_2d | Aura | sustained | P1 | 上升像素十字与绿点，4 级闪烁 |
| anime_smear_slash_2d | Slash | one-shot | P2 | 手绘 smear 刀光帧动画+速度线 |
| poof_smoke_spawn_2d | Spawn | one-shot | P2 | 卡通烟团 poof 出场（忍者消失烟） |
| anime_charge_aura_2d | Aura | sustained | P2 | 少年漫蓄力气场：帧动画火苗轮廓+上升粒 |
| ink_slash_2d | Slash | one-shot | P3 | 一笔挥毫刀光，飞白收笔 |
| ink_splash_impact_2d | Impact | one-shot | P3 | 墨滴砸落晕染+溅点 |
| ink_dragon_trail_2d | Trail | event | P3 | 游龙墨迹尾迹，头部龙形笔触 |

## 3. 规格卡（每管线一张详卡 + 两张简卡）

### pixel_burst_impact_2d（P1 详卡）
- 分层：主体=白闪 1 帧（量化后 2×2 大像素块）→ 橙色爆团（12fps 跳帧膨胀 4 帧）；高光=爆团内亮黄块面；外部能量=烟圈灰阶 3 帧；次级=8 向飞散像素点（直线运动，无重力弧线——像素风惯例）；消散=烟圈最后 1 帧整体消失（不淡出，量化 Alpha）。
- 参数：virtual_res 64/96/128、palette_lut（暖火/寒冰/毒紫）、burst_scale、debris_count 4–12。
- 预算：粒子 ≤24 / PS ≤2 / 材质 ≤3 / Renderer ≤4。
- 验收帧：12fps 采样下逐"格"检查，任何两相邻采样帧不得出现平滑插值痕迹。

### pixel_sword_slash_2d — 3 帧刀光弧（宽→窄→碎），命中 4 角星迸点；参数：arc_frames、star_count；预算 PS ≤2。
### pixel_heal_aura_2d — 绿色十字与圆点匀速上升，4 级 Alpha 闪烁循环；参数：symbol_mix、rise_speed；预算 PS ≤2。

### anime_smear_slash_2d（P2 详卡）
- 分层：主体=8 帧手绘 smear 弧光图集（起手拉伸帧→峰值宽帧→收尾细帧）；高光=第 3–4 帧叠加白芯层；外部能量=速度线 3–5 条（程序 Mesh）；次级=峰值帧迸出 4–6 颗手绘星火（图集子帧）；消散=末 2 帧残影降不透明度。
- 参数：atlas_fps 18–30、smear_scale、speedline_count、palette。
- 预算：粒子 ≤24 / PS ≤2 / 材质 ≤4 / Renderer ≤6。
- 图集：`SlashSmearAtlas_A`（8+6 子帧，1024²，登记 SHA/来源）。

### poof_smoke_spawn_2d — 6 帧烟团开花图集+外围 4 小团错峰，实体在第 3 帧时点切换可见；参数：poof_scale、satellite_count；预算 PS ≤2。
### anime_charge_aura_2d — 8 帧循环火苗轮廓包体+上升粒点、地面风圈；参数：flame_atlas_row（2 色系）、intensity 1–3；预算 PS ≤3。

### ink_slash_2d（P3 详卡）
- 分层：主体=单笔新月墨迹（笔刷遮罩 Reveal，起笔重、收笔飞白）；高光=笔锋处纸白挤出（负空间）；外部能量=边缘晕染向外渗 0.2s；次级=甩出墨点 3–5 滴；消散=整体如宣纸吸墨般变淡（bleed_radius 增大同时 density 下降）。
- 参数：stroke_width、flyaway_threshold、bleed_radius、accent_color（默认朱红一点）。
- 预算：粒子 ≤16 / PS ≤1 / 材质 ≤3 / Renderer ≤5。

### ink_splash_impact_2d — 墨滴坠落 1 帧拉伸→冠状溅开→晕染圆扩散；参数：splash_count、bleed_time；预算 PS ≤2。
### ink_dragon_trail_2d — 运动头为龙首笔触（2 帧摆动），身后墨带宽度随速度、尾端持续晕散；参数：body_width、fade_bleed；预算 PS ≤2。

## 4. 批次验收
通用 DoD + 附加：三条管线各出一份"管线使用说明"进 `docs/ai-workflow/`（模板参数生成文档同步）；用 P1 链给 `fireball_2d` 出 `.pixel` 变体作为管线复用证明；水墨三效果色域校验（超出墨阶+单点缀即报错）。

## 5. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W9 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造像素、手绘或水墨的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 6. 后续独立 next-candidate（2026-08-25）

用户后续已授权全项目继续开发；旧候选、旧 Scene 与上述拒绝记录继续保持不变。本轮另建 W9 源码 `next_candidate`：10 个条目分别使用真实 `pixel/cartoon/inkwash` Shared Material 与 3–5 个有界 Mesh 载体，运行时公开 12fps/18fps 离散相位、Material 命中次数、强度、可见层数和 envelope 状态。未来隔离构建目标为 `VFXPREVIEW_Style2D_NextCandidate.unity`，当前只完成源码与静态门禁，状态为 `NEXT_CANDIDATE_VISUAL_PENDING`；没有新的用户视觉结论。详见 `docs/stage-notes/W9_W10_W16_NEXT_CANDIDATE_REPORT.md`。
