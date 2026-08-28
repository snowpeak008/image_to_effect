# W4 冰霜族扩展（7 个特效）

> 实现状态（2026-08-25）：7/7 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：补全冰霜族（现仅 frost_impact_2d、snow_weather_volume 及 Frost 素材库），复用 `ArtSource/VFX/Frost` 已清洗模块与 `Shared/Frost/`。
> 批次预览场景：`VFXPREVIEW_FrostFamily.unity`。
> 配色基准：主 `#7FD8FF` / 辅 `#FFFFFF` / 点缀 `#2E6BD6`；dark 变体主 `#9FB8D8`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| ice_spike_spawn_3d | Spawn | 3D | one-shot | stylized | 地面预兆冰纹 → 冰刺群破土 |
| blizzard_area_3d | Area | 3D | sustained | semireal | 区域暴风雪，斜向雪粒与寒雾 |
| frost_breath_beam_2d | Beam | 2D | sustained | cartoon | 扇形吐息冰雾 |
| ice_shard_projectile_2d | Projectile | 2D | event | stylized | 旋转冰锥，命中碎裂 |
| freeze_status_2d | Aura(状态) | 2D | sustained | stylized | 冰冻控制：包裹冰壳+寒气 |
| crystal_shield_3d | Shield | 3D | event | stylized | 六棱冰晶花瓣环身护盾 |
| flash_freeze_transform_3d | Transform | 3D | one-shot | stylized | 瞬间冰封定身再碎裂解除 |

## 2. 规格卡

### ice_spike_spawn_3d
- 分层：主体=3–7 根锥形冰刺 Mesh 依次破土（缩放+轻微过冲）；高光=刺尖高光与内部折射纹（Fresnel）；外部能量=破土雪尘环；次级=碎冰弹跳粒子；消散=冰刺下沉+碎裂二选一（参数）。
- 时间线：`.0–.15` 地面冰纹预兆 → `.15–.35` 依次破土 → 保持 0.5s → 消散。
- 参数：spike_count 3–7、height 0.5–2.5m、pattern（线/扇/环）、exit_mode（sink/shatter）。
- 预算：粒子 ≤48 / PS ≤3 / 材质 ≤4 / Renderer ≤7；斜视角验证刺体真实体积。

### blizzard_area_3d
- 分层：主体=区域内斜向奔流雪粒（风向参数）；高光=近景大雪片闪烁；外部能量=贴地寒雾滚动层；次级=偶发冰晶闪光；消散=风势渐弱 1s 收尾。
- 参数：radius 3–10m、wind_dir、density、fog_height。
- 预算：粒子 ≤120（区域放宽档，Manifest 声明）/ PS ≤4 / 材质 ≤4 / Renderer ≤6。

### frost_breath_beam_2d
- 分层：主体=扇形冰雾喷流（笔刷遮罩滚动）；高光=近口白芯；外部能量=雾缘散逸小旋涡；次级=悬浮冰晶星点；消散=停止后雾体前飘散尽。
- cartoon：3 级色阶、外描边、雾团轮廓卡通化。
- 参数：cone_angle 30–70°、length、crystal_density。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤3 / Renderer ≤6。

### ice_shard_projectile_2d
- 分层：主体=旋转冰锥（复用 Frost Shard 模块图）；高光=棱线闪光；外部能量=寒气拖尾；次级=沿途冰晶点；消散=Impact 放射碎冰+霜环（衔接 frost_impact_2d 语义，但为独立 Recipe）。
- 参数：spin_speed、trail_length、shard_variant 1–4（用现有 4 张 Shard 模块）。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### freeze_status_2d
- 分层：主体=包裹角色的冰壳剪影（半透明多边形壳）；高光=壳面棱光扫过；外部能量=脚底霜环蔓延；次级=下落寒气丝；消散=解冻时壳体碎裂 6–10 块坠落。
- 参数：shell_opacity、duration、shatter_piece_count；挂点协议同 Status。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤4 / Renderer ≤6。

### crystal_shield_3d
- 分层：主体=6 片六棱冰晶花瓣环身缓旋；高光=晶面 Fresnel 折射；外部能量=花瓣间寒雾连接；次级=表面剥落细雪；消散=花瓣依次碎裂。
- 事件：OnHit → 面向命中方向的花瓣亮起+霜花放射。
- 参数：petal_count 4–8、orbit_radius、hit_flash_color。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤5 / Renderer ≤7。

### flash_freeze_transform_3d
- 分层：主体=从脚到头的冰封包裹波（高度 Reveal）；高光=封顶瞬间白闪；外部能量=封体表面霜纹生长；次级=封固时逸散寒气；消散=保持后整体碎裂为冰块粒子+雪尘。
- 时间线：`.0–.25` 上升冰封 → 保持（参数）→ `.15` 内碎裂清空。
- 参数：freeze_duration、rise_speed、shatter_scale。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

## 3. 批次验收
通用 DoD + 附加：blizzard 与 freeze_status 必须验证 sustained 三周期稳定与 Stop 干净；flash_freeze 的碎裂帧必须无"闪烁重现"（沿用 S15 消散趋势标准）；至少 2 个 dark/inkwash 风格变体。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将本项统一签署为：**拒绝，无法商用**。本 Scene 未作逐格/逐帧视觉核对，不据此伪造具体冰霜画面问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. 后续 W4 next-candidate（视觉待签署）

后续独立任务已建立新路径；本节不改写第 4 节批量拒绝或补造旧画面问题。新 Scene 为 `Assets/VFX/Preview/VFXPREVIEW_FrostFamily_NextCandidate.unity`，状态根必须保持 `W4_NEXT_CANDIDATE_VISUAL_PENDING`，输出使用独立的 `Assets/VFX/NextCandidates/W3W5Elements/` 和 compiler `element-next-w3-w5-1`。

- 新 Runtime executor 把结晶生长、几何棱面锐度、霜雾、碎裂和融解/下沉作为不同阶段与载体；不是把火系 Mesh 改成蓝色。
- 7 个效果各有程序化形状：可选 line/fan/ring 的冰刺群、风切雪幕、棱面吐息扇、variant 冰棱、冻结多边壳、晶体花瓣和竖向生长冰封壳。晶体 body 使用 alpha/facet 表达，柔软霜雾独立 alpha 层，高光才使用 additive。
- 冰刺数量/高度/排布/退出方式、暴雪半径/风向/密度/雾高、吐息角度/长度/晶体数、冰棱转速/拖尾/variant、冻结透明度/保持时间/碎片数、晶盾花瓣/轨道半径/`hit_flash_color`、闪冻保持/上升速度/碎裂尺度均进入实体几何、粒子、事件或时序。`hit_flash_color` 直接驱动受击 EventCarrier 的 Renderer 属性块。
- 固定上限为 7 Renderer、1 ParticleSystem、单 Prefab 粒子不超过 120；使用确定性粒子批、无 Rigidbody、Immediate/AllowTail 与池化 Reset。

新增门禁会检查结晶生长、霜雾、锐度、shatter 与 sink/melt 分支、内容参数到实体 carrier、预算、清理复播、幂等构建以及 Preview 跨格/标签边界。当前仅完成静态编译，未启动 Unity、未执行测试 XML 或视觉签署；状态仍为 `VISUAL_PENDING`。
