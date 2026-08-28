# W6 水系与风系扩展（8 个特效）

> 实现状态（2026-08-25）：8/8 默认 Recipe、语义 Patch、strict Runtime Entry 和批次 Preview 已完成机器门禁；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。统一证据见 `docs/stage-notes/W3_W8_ELEMENT_FAMILIES_REPORT.md`。

> 目标：新建两个全空白元素族。水系核心是**体积感与流动**（拉丝、飞溅、泡沫三件套）；风系核心是**用被卷起的介质表现看不见的风**（尘、叶、气流线），禁止把风做成白色实体带。
> 批次预览场景：`VFXPREVIEW_WaterWindFamily.unity`。
> 配色：水主 `#4DA6FF` / 泡沫 `#EAF8FF`；风主 `#CFEFE0` / 尘 `#D8CBA8`。

## 1. 清单

| id | Archetype | 维度 | 生命周期 | 基准风格 | 一句话 |
|---|---|---|---|---|---|
| water_jet_beam_3d | Beam | 3D | sustained | stylized | 高压水柱，端点飞溅 |
| tidal_wave_area_3d | Area | 3D | one-shot | stylized | 横扫浪墙，过境留水渍 |
| bubble_shield_2d | Shield | 2D | event | cartoon | 水泡护盾，破裂喷溅 |
| splash_impact_2d | Impact | 2D | one-shot | cartoon | 水花冠状飞溅 |
| whirlpool_spawn_3d | Spawn | 3D | one-shot | stylized | 漩涡成形召唤入场 |
| tornado_area_3d | Area | 3D | sustained | stylized | 移动龙卷，卷起碎屑 |
| wind_blade_slash_2d | Slash | 2D | one-shot | inkwash | 镰鼬风刃，气流线新月 |
| gale_dash_trail_2d | Trail | 2D | event | stylized | 疾风冲刺，人形残影+流线 |

## 2. 规格卡

### water_jet_beam_3d
- 分层：主体=圆柱水流 Mesh（纵向流动 UV+边缘扰动）；高光=水芯高光丝；外部能量=沿柱剥离水珠；次级=端点冠状飞溅+雾化；消散=停止后水柱断裂坠落感（重力下垂 0.3s）。
- 参数：length、pressure（影响粗细/飞溅量）、foam_amount。
- 预算：粒子 ≤72 / PS ≤4 / 材质 ≤4 / Renderer ≤6。

### tidal_wave_area_3d
- 分层：主体=弧形浪墙 Mesh 前推（顶部卷曲）；高光=浪脊泡沫带；外部能量=墙前飞溅帘；次级=过境地面水渍层（0.8s 淡出）；消散=浪墙塌落成低飞沫。
- 时间线：`.0–.2` 隆起 → `.2–.7` 前推 4–8m → `.7–1.0` 塌落 → 水渍余留。
- 参数：wave_width 2–6m、travel_distance、curl_amount。
- 预算：粒子 ≤96（区域放宽档）/ PS ≤4 / 材质 ≤5 / Renderer ≤7。

### bubble_shield_2d
- 分层：主体=大水泡壳（彩虹薄膜高光偏移）；高光=顶部月牙反光；外部能量=表面小泡缓升；次级=底缘滴落水珠；消散=破裂帧（1 帧膨胀+尖刺轮廓）+全向水花。
- cartoon：厚描边、反光块面化、破裂用经典"爆星"过渡帧。
- 参数：bubble_radius、wobble（果冻抖动幅度）、pop_splash_scale。
- 预算：粒子 ≤32 / PS ≤2 / 材质 ≤3 / Renderer ≤5。

### splash_impact_2d
- 分层：主体=皇冠状水花（8–12 齿轮廓 Mesh 一帧成形再回落）；高光=齿尖水珠；外部能量=外扩水环；次级=回落细滴；消散=地面水渍圆淡出。
- 参数：crown_scale、droplet_count、ring_count 1–2。
- 预算：粒子 ≤40 / PS ≤2 / 材质 ≤3 / Renderer ≤5。

### whirlpool_spawn_3d
- 分层：主体=地面漩涡盘（螺旋 UV 加速旋转）；高光=螺旋臂亮线；外部能量=中心升起水柱预告实体出现；次级=边缘甩出水珠；消散=实体出现时点水柱炸开、漩涡收拢。
- 参数：vortex_radius、spin_accel、column_height；实体出现时点为外部事件（沿用 Spawn 协议，不生成 Gameplay 对象）。
- 预算：粒子 ≤56 / PS ≤3 / 材质 ≤4 / Renderer ≤6。

### tornado_area_3d
- 分层：主体=锥形旋风体（2 层反向旋转噪声带，下窄上宽）；高光=风体棱线偶现；外部能量=底部卷起尘环+碎屑螺旋上升；次级=顶部甩出叶片/碎屑；消散=停止时风体上收、碎屑坠落。
- 关键：风体本身低不透明度（≤0.35），存在感靠碎屑与尘环。
- 参数：height 2–5m、move_speed、debris_type（尘/叶/雪，复用共享图集）。
- 预算：粒子 ≤88（区域放宽档）/ PS ≤4 / 材质 ≤4 / Renderer ≤6。

### wind_blade_slash_2d
- 分层：主体=细长新月气流线（3 条平行细弧，中粗边细）；高光=主弧前缘；外部能量=弧后拖出短流线；次级=切过处扬起 2–3 片叶/尘；消散=流线尾端飘散。
- inkwash：墨线飞白边缘，低饱和+叶片单点绿。
- 参数：blade_count 1–3、arc_length、leaf_count。
- 预算：粒子 ≤24 / PS ≤2 / 材质 ≤3 / Renderer ≤6。

### gale_dash_trail_2d
- 分层：主体=冲刺路径水平流线束（速度线，长短错落）；高光=主流线亮芯；外部能量=起点爆风圈+终点刹停尘团；次级=路径人形残影 2–3 帧（外部提供剪影 Mesh 接口，缺省用胶囊）；消散=流线从尾向头收缩。
- 参数：dash_length、afterimage_count 0–3、line_density。
- 预算：粒子 ≤40 / PS ≤3 / 材质 ≤4 / Renderer ≤7。

## 3. 批次验收
通用 DoD + 附加：tornado/tidal_wave 斜视角验证体积；风系三效果通过"隐形介质"人工检查项（风的存在完全由碎屑/流线表达）；至少 1 个 Patch 示例（建议 tornado 换 debris_type）。

## 4. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将本项统一签署为：**拒绝，无法商用**。本 Scene 未作逐格/逐帧视觉核对，不据此伪造具体水/风画面问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 5. W6 全新 next-candidate 追加（2026-08-25）

本节只记录后续明确授权的新候选；上面的旧候选、逐字拒绝记录和旧证据均未修改。新候选使用编译器版本 `element-next-w6-w8-1`、独立输出根 `Assets/VFX/NextCandidates/W6W8Elements/`、新 Scene `VFXPREVIEW_WaterWindFamily_NextCandidate.unity`，Scene 状态根为 `W6_NEXT_CANDIDATE_VISUAL_PENDING`。

- 水不再是通用 Mesh 换蓝色：水柱有压力控制的多股拉丝、芯部高光、泡沫/端点飞溅和停止后 0.3s 下垂；浪墙执行隆起、卷曲前推、飞溅帘、塌落与水渍；泡盾、皇冠水花与漩涡分别使用薄膜壳/爆星、冠齿/水环和加速螺旋/水柱程序化载体。
- 风保持低不透明度（运行读回上限 0.35），龙卷的存在由尘/叶/雪介质、螺旋上升和底部尘环表达；风刃是 1–3 条细弧而非白色实体带；冲刺用长短流线、残影层数及起止尘表达。
- `length/pressure/foam_amount`、`wave_width/travel_distance/curl_amount`、`debris_type`、`blade_count/arc_length`、`dash_length/afterimage_count/line_density` 等全部进入明确的几何、粒子计数或时序 carrier；每项拥有自己的程序化 Mesh asset，不复用旧固定 body Mesh。
- 固定上限仍为 7 Renderer、1 个池化 ParticleSystem、120 粒子和 3 个实际共享材质；各 profile 再按本规格收紧到 5–7 Renderer、24–96 粒子。Body/介质使用 alpha blend，仅高光使用 additive。

未来隔离 Unity 执行入口：`VFXComposer.Editor.Elements.ElementNextCandidateW6W8Authoring.BuildW6ForBatch`；只建单元素族可用 `BuildWaterForBatch` 或 `BuildWindForBatch`。定向过滤器：EditMode `VFXComposer.Tests.EditMode.W6W8ElementNextCandidateTests`，PlayMode `VFXComposer.Tests.PlayMode.W6W8ElementNextCandidateRuntimeTests`。当前只完成源码与 Bee/Roslyn 静态编译，未启动 Unity、未写测试 XML、未作用户视觉签署；机器门禁不得将状态改成视觉通过，新候选保持 `VISUAL_PENDING`。
