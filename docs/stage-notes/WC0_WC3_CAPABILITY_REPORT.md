# W-C0–W-C3 能力层开发报告

日期：2026-08-24  
状态：**机器开发完成；W-C1、W-C2、W-C3 的旧能力预览均已被用户视觉拒绝；W24 后续授权的新候选保持视觉待签**  
范围：`docs/allwork/20–23`，不包含 W1 风格渲染实现，也不代签 W0 或任何视觉结论。

## 1. 结论

本轮已经把项目从“按元素手写单个特效”扩展为“能力机制与元素/风格正交”的能力层：

- Recipe 仍使用兼容字段 `recipeVersion: 1`，一次性加入 1.2 契约的可选 `behavior` 与对象形式 `style`，旧 Recipe 不需要迁移即可继续解析、校验和构建。
- `behavior.motion / hit / emission / timing` 使用登记 token、参数类型/范围和 Archetype 白名单；非法组合有稳定路径与错误码。
- Runtime 提供不依赖渲染的 `SampleTrajectory`，输出轨迹、端点、进度和事件；同 Recipe、seed、步长的 canonical JSON 逐字节一致。
- 先登记既有实现，再新增素体：`seeker_orb_3d`、`chain_arc_3d`、`channel_tether_3d`、`warning_telegraph_3d`、`static_field`、`phase_dash_3d` 和 `chain_blast` 均进入能力登记，不重复制作旧资产。
- 已产生 30 个计划内能力素体 Runtime Entry；另有 `cap_hexflash_impact_2d` 与 `cap_residue_trail_3d` 两个视觉槽支撑 Entry，所以 `Recipes/Capability` 与 `Generated/cap_*` 实际各有 32 项。

## 2. W-C0：Recipe 1.2 兼容迁移与能力基础设施

实现：

- `VfxDomainParser` / `VfxDomainModels`：解析可选 `behavior`；`style` 接受新对象形式，并兼容旧字符串 `"stylized"`。
- `CapabilityRegistry`：登记 motion/hit/emission/timing token、参数契约、Archetype 适用范围、旧实现来源和组合规则。
- `CapabilitySampler`：纯 C# 逻辑采样，记录 position/velocity/source/target/progress/events，不读取 Camera、Renderer 或 Scene。
- Patch 新增 `set_behavior_param`、`set_style_token`、`set_palette`；能力 `type` 仍禁止 Patch 切换，避免局部修改伪装模板重建。
- Schema 文档、AI 作者说明、升级说明与错误码已同步。新增稳定错误码 `E320–E329`。

兼容性门禁：旧 fireball Recipe 无 `behavior` 仍合法；旧 style 字符串仍可解析；新对象字段和参数类型/范围受严格校验。

## 3. W-C1：弹道能力

12 个素体覆盖：

1. `motion.linear`
2. `motion.accel`
3. `motion.parabola`
4. `motion.homing`
5. `motion.wave`
6. `motion.boomerang`
7. `motion.bounce`
8. `motion.orbit_then_strike`
9. `hit.pierce`
10. `hit.split`
11. `hit.chain_hop`
12. `emission.fan / burst_stagger / ring` 的统一 volley 素体

机器验收包含：速度/加速度/顶点/落点、受限转向、往返状态、反弹法向与能量衰减、命中次数、子弹阵型、跳跃目标、三种发射阵型、跨子块组合、非法组合、同 seed 字节确定性、正式 Prefab 可加载、二次构建文件哈希与 GUID 不变。

### 3.1 最终用户视觉签署（2026-08-25）

用户对 `Assets/VFX/Preview/VFXPREVIEW_CapProjectile.unity` 的结论为**拒绝**：“格10未显示5个子弹，格11未依次跳转4个目标，格12未显示扇形且缺少错峰/环形模式。”

| 格 | 时间阶段 | 原计划预期 | 当前实际 |
|---|---|---|---|
| 10 Split | 场景周期约 `1.08s` | 分裂为 5 个子弹，总夹角 `80°`，子核缩小至 `60%` | 逻辑采样产生 5 条同帧事件，但运行画面只有单一 Core/单一 Marker，未生成 5 个可见子弹 |
| 11 Chainhop | 场景周期约 `0.78s` | 依次跳向 4 个目标，每跳有命中反馈和衰减 | 4 条 `on_hit` 在同一帧生成并覆盖同一 Marker；核心仍直线运动，未形成分时目标跳转 |
| 12 Volley | 场景周期约 `0.08s` | 5 发、`50°` 扇形，并可区分 `fan / burst_stagger / ring` | `on_emit` 方向未被渲染器实例化，画面只有单枚直线 Core；预览也没有错峰/环形模式切换 |

直接技术原因是中性素体 Prefab 只实例化一个 Core 和一个事件 Marker，而多子弹/多目标方向仅保留在逻辑 Trace 事件中，没有接到多实例视觉执行层。现有机器测试证明事件数量、方向数据和确定性，不证明这些事件已按原计划渲染。

上述旧 W-C1 候选状态永久保留为 `rejected`。当时“未授权重做”是历史事实；随后用户已
授权按 W24 全项目继续开发，因此另建下一候选，而不是改写旧拒绝。

### 3.2 W-C1 下一候选（机器完成，视觉待签）

- Split：一个手动批量 ParticleSystem 在 `on_split` 后维持 5 枚 60% 子核，方向精确
  读取 Trace，阵型为 `-40/-20/0/20/40°`；母核退出，Stop/Reset 清零。
- Chainhop：四跳改为 `0.70/0.96/1.22/1.48s` 严格递增事件，Core 分段移向四个不同
  目标，反馈按 `0.85^hop` 衰减，不再同帧覆盖单一 Marker。
- Volley：Recipe revision 2 显式声明 fan 5 发/50°、burst 5 发/0.09s 错峰、ring 8 发/45°
  三阶段，运行载体逐阶段读回，而非 Preview 手写动画。
- 统一预算：Renderer ≤4、PS ≤2、粒子上限 ≤24、材质 ≤2；Preview 标记为
  `W_C1_NEXT_CANDIDATE_VISUAL_PENDING`，禁止出现 accepted。

隔离 Unity 已完成物化；`ProjectileCapabilityTests` `7/7`、W-C1-only Preview 测试
`1/1`、`ProjectileCapabilityRuntimeTests` `2/2` 通过。该结论只关闭格 10–12 的已知
执行层缺口；新候选仍为 `VISUAL_PENDING`，没有用户通过、L3 或 L4。

## 4. W-C2：射线能力

8 个素体覆盖：`hitscan`、`sustained`、`sweep`、`charge_scale`、`reflect`、`occlude`、`converge`、`arc_link`。

机器验收仅覆盖纯采样/事件契约与基础两点直线生命周期，包含：

- hitscan 在触发帧发出命中事件；
- sustained 在人工传入 `TargetVelocity` 的采样请求中更新 Target；正式 Prefab/Scene 没有端点跟随驱动；
- sweep 只断言相邻帧角变化不超过上限，没有断言端点实际发生非零扫动；
- charge 的 Trace 宽度层级；
- reflect 的方向、事件数和事件向量衰减，不包含可见反射折线；
- occlude 在人工输入两组障碍距离时的合成端点变化，不包含 Scene 物理遮挡；
- converge 的逻辑发射事件端点一致；
- arc-link 的逻辑拓扑事件；
- `charge_scale + occlude` 等组合冒烟以及非法组合拦截；
- 8 个正式 Runtime Entry 幂等构建与 Player-safe 加载。

### 4.1 最终用户视觉签署（2026-08-25）

用户对 `Assets/VFX/Preview/VFXPREVIEW_CapBeam.unity` 的结论为**拒绝**。用户原话：“拒绝；格1缺少衰减，格2缺少端点跟随，格3无扫射，格4仅变粗，格5无反射多段，格6无烧灼点和遮挡变化，格7无四源汇聚，格8无跳线。”

用户未另行给出具体时间点；下表时间阶段来自本次签署前按该 Scene 的 `4.2s` 复播周期所做的只读技术核对，不扩写为用户逐帧原话。

| 格 | 时间阶段 | 原计划预期 | 当前实际 |
|---|---|---|---|
| 1 Hitscan | 场景周期约 `0.08–0.23s` | 触发帧整束出现并即时标记命中点，随后在 `0.1–0.2s` 内衰减消失 | 整束和通用端点环同帧出现，但宽度/颜色不衰减；约 `0.15s` 后直接关闭 |
| 2 Sustained | 场景周期约 `0.08–3.08s` | Start/Stop、Source/Target 端点跟随，并验证长度变化时的平铺 | 只有固定 Source→Target 的两点直线和自动生命周期；没有移动端点或独立 Start/Stop 演示 |
| 3 Sweep | 场景周期约 `0.08–2.08s` | 端点沿外部路径扫动，体现惯性/最大角速度并切换扫过目标 | Sweep 计算结果同帧被固定 sustained Target 覆盖，画面保持静止，无扫射 |
| 4 Charge Scale | 场景周期约 `0.08–2.08s`；宽度约在 `0.58s`、`1.18s` 升级 | 三级束宽、亮度和终段爆点同步变化，并有蓄力取消路径 | 只显示三级变粗；没有亮度变化、终段爆点或 Scene 内取消演示 |
| 5 Reflect | 场景周期约 `0.08–2.08s`；起点环约在 `0.58s / 1.08s / 1.58s` 闪现 | 最多 N 段可见反射折线、折点反馈和逐段宽度/亮度衰减 | 始终只有一段固定两点直线；三个 `on_bounce` 只覆盖同一通用 Marker，并在起点闪三次 |
| 6 Occlude | 场景周期起始约 `0.08s` | 首个阻挡物截断、阻挡点烧灼，移开障碍后束体在两帧内延伸 | 没有物理遮挡检测、移动遮挡柱或烧灼点组合；只有固定合成截断端点 |
| 7 Converge | 场景周期起始约 `0.08s` | 四个环布源点各发一束并汇于同一焦点，焦点随功率增长 | 四条 `on_emit` 只存在 Trace 中且未被渲染器消费；画面只有单源单线，无汇点增长 |
| 8 Arc Link | 场景周期约 `0.78s` 产生跳跃事件 | 依次形成多段跳线，并表现 sag/jitter | 四条 `on_hit` 同帧覆盖同一 Marker；画面仍是一段固定两点线，没有跳线、下垂或抖动 |

直接技术原因是中性 Beam Prefab 只生成一个 `LineRenderer` 和一个通用事件 Marker，运行控制器又把所有 Beam 强制为两个端点；正式 Scene 没有移动靶、反射墙、遮挡柱或独立端点驱动。逻辑 Trace 中的多源、反射和跳线事件没有接入对应的多段/多实例视觉执行层，字符串视觉槽也没有形成计划中的端点、折点或烧灼效果。现有测试证明纯采样事件、确定性和两点直线生命周期，不证明上述视觉要求；仓库中也没有 CapBeam 截图、视频或逐帧视觉证据。

当前 W-C2 候选状态为 `rejected`。本次只记录用户拒绝、逐格现象与已确认技术原因；用户未授权重做、修改源码/资产或生成下一候选。

### 4.2 后续 W24 授权下的 W-C2 下一候选（机器物化完成，视觉待签）

4.1 的拒绝候选及用户原话保持不变；本节另立后续授权的候选。状态仅为 `next_candidate / VISUAL_PENDING`，Scene 状态根协议为 `W_C2_NEXT_CANDIDATE_VISUAL_PENDING`，不代表旧拒绝被覆写或已产生新的用户视觉结论。隔离 Unity 已重建八个 Prefab 与 `VFXPREVIEW_CapBeam.unity`，compiler version 为 `capability-blank-3-beam-visual-execution`；未触碰 S0a/S0b/S3 或 W24 正式证据。

源码已把八项缺口接到真实 Runtime 执行层：hitscan 的整束同帧出现、端点标记及 `0.1–0.2s` alpha/宽度淡出；sustained 的公开 Start/Stop、Source/Target Transform/坐标协议及长度平铺；sweep 的非零弧、角速度上限与惯性；charge 的三级宽度/亮度、完成爆点与可见取消出口；reflect 的 Trace 驱动多 LineRenderer 折线、折点粒子与逐段衰减；occlude 的显式 Collider probe、无 probe fail-closed、首阻挡/burn point 与移障两帧响应；converge 的四条环布源线及增长焦点；arc-link 的严格递增 hop 时序与带 sag/jitter 的有界多点线。Preview Driver 只提供移动端点、移动阻挡物和取消/复播外部输入，不承载这些语义。

当前固定上限：普通格 `3 Renderer / 0 PS`，reflect `5 Renderer / 1 PS / ≤16 粒子`，converge `6 Renderer / 0 PS`，每个 Prefab 一个共享中性材质引用；无逐帧 Instantiate。Stop/Reset 必须清空 LineRenderer、粒子、标记、端点协议与遮挡状态。`CapabilityBlankCompiler.BuildBeamBlanks` 使用独立 Beam compiler version 且仅重建稳定的八个 Beam ID；二次构建测试保留 GUID/hash 幂等门禁，不影响 W-C1 split/chain/volley 载体与预算。

隔离机器结果：`BeamCapabilityTests` `5/5`、`BeamCapabilityRuntimeTests` `9/9`、W-C2-only Preview `1/1`；共享 W-C1 回归 `ProjectileCapabilityTests` `7/7`、`ProjectileCapabilityRuntimeTests` `2/2`、W-C1-only Preview `1/1`。这些结果证明 Runtime 接线、预算、清理、场景状态根与共享控制器回归，不授予视觉结论；新候选继续保持 `VISUAL_PENDING`。

## 5. W-C3：时序、范围与组合槽

10 个素体覆盖：`telegraph`、`delay_fuse`、`tick_pulse`、`charge_release`、`channel_interrupt`、`chain_sequence`、`expand_ring`、`implode`、`moving_zone`、`growth_stage`。

机器验收只覆盖纯采样/事件契约：预警进度数值、延爆 Width 数值、Tick 等间隔事件、蓄力等级和释放事件、Channel 正常完成与取消的出口字符串、连锁事件时空序列、扩张/收缩半径、聚爆前 Stage、移动区域位置数值和成长阶段事件；它不证明这些数据已映射成可见、可区分或美观的运行画面。

视觉槽采用保存 Recipe ID：构建前解析依赖、只允许一层、不允许自引用/环和错误 Archetype。`impact_slot`、`tick_visual_slot`、`residue_slot` 已进入 Validator、DryRun、Build 与 Patch 后验证；错误使用分别由 `E328/E329` 定位。该门禁只证明引用合法，不证明当前 Capability Blank Prefab 已实例化槽内视觉。

### 5.1 最终用户视觉签署（2026-08-25）

用户对 `Assets/VFX/Preview/VFXPREVIEW_CapTiming.unity` 的结论为**拒绝**。用户原话：“拒绝；W-C3仅完成逻辑采样，10格同时播放且跨格重叠、裁切严重；各格共用同色圆环，缺少预警爆发、加速闪烁、Tick视觉、三级蓄力、双出口、独立连锁爆点、聚爆、残留和升级脉冲等原计划视觉表达。”

用户未另行给出逐格时间点；下表时间阶段来自本次签署前按该 Scene 的 `4.2s` 复播周期所做的只读技术与视觉自检，不扩写为用户逐帧原话。

| 格 | 时间阶段 | 原计划预期 | 当前实际 |
|---|---|---|---|
| 1 Telegraph | 场景约 `0.08–1.28s`；预警结束约 `0.88s` | 形状/填充可读，进度结束同帧爆发并调用 Impact 槽 | 只有中心球、放大圆环和通用小环闪；无中心填充、形状差异或真正爆发 |
| 2 Delay Fuse | 场景约 `0.08–1.38s`；引爆约 `1.08s` | 闪烁频率递增后准确引爆 | `blink_accelerate` 只改变未被 Area 渲染读取的 Width；画面近似格 1 慢版，无加速闪烁 |
| 3 Tick Pulse | Tick 约在 `0.48 / 0.88 / 1.28 / 1.68 / 2.08s` | 五次等间隔 Tick，每次调用独立视觉槽 | `on_tick` 不在可见事件白名单，槽视觉未实例化；只有同一圆环周期缩放 |
| 4 Charge Release | 等级约在 `0.48 / 0.98s`；默认释放约 `1.48s` | 三级增强、满级提示、按等级释放，并有 Release/Cancel 双出口 | Width/等级事件没有进入 Area 视觉；无三级跃迁、满级提示、可见释放或取消演示 |
| 5 Channel | 正常完成约 `2.08s`，约 `2.28s` 熄灭 | 引导进度；正常收束与打断溃散为两个清楚不同的视觉出口 | 只有线性进度圆环；Scene 不触发 Cancel，Complete/Cancel 最终都是立即隐藏 |
| 6 Chain Sequence | 爆点约在 `0.28 / 0.48 / 0.68 / 0.88 / 1.08s` | 五个独立爆点按直线拓扑接力并调用 Impact 槽 | 同一个通用事件环向右跳动，不是五个独立爆点；轨迹横跨相邻格 |
| 7 Expand Ring | 场景约 `0.08–1.58s`；半程命中约 `0.75s` | 边界连续外扩，边界经过处有命中层 | 只有半径最大为 `4` 的巨大单色环和一次通用 Marker；覆盖多格、多排 |
| 8 Implode | 收缩约 `0.08–1.08s`；屏息到 `1.18s` 后释放 | 外环收缩、`0.1s` 屏息、中心聚爆 | 屏息 Stage 未渲染；半径归零时 fallback 造成缩放回跳，中心仅复用小事件环 |
| 9 Moving Zone | 场景约 `0.08–2.08s` | 沿外部路径移动、边界平滑形变、路径残留与 Complete/Cancel 双出口 | 只趋近固定局部 Target；无外部路径、残留或分支出口，并移动进格 10 区域 |
| 10 Growth Stage | 阶段约在 `0.08 / 0.78 / 1.48s`；约 `2.18s` 完成 | 小→中→大，边界与内部密度跃迁并有升级脉冲 | 只有同色圆环按 `1→2→3` 跳尺寸；无内部密度变化或升级脉冲，并发生跨格/裁切 |

整体构图的直接技术原因：10 个 Entry 在每轮约 `0.08s` 同时 Play，没有错峰、单格聚焦、遮罩或格内裁剪；格距仅 `2.7 × 2.35`，而格 7/8 半径为 `4`、格 10 半径为 `3`。所有实例还相对标签统一左移 `1.4`，使第 1/6 格效果锚点落到 16:9 相机视野外，外侧标签也被裁切。所有素体共用灰蓝加法材质、一个中心球、一个边界环和一个通用事件环，跨格后继续叠亮且无法辨认归属。

现有机器测试只断言采样数值、事件、出口字符串、Renderer 数量和 Scene Cell 数量；没有 W-C3 截图、像素、跨格遮挡/裁切、视觉槽实例化、双出口演出或风格可辨识性断言。因此机器通过不构成视觉通过。

当前 W-C3 候选状态为 `rejected`。本次只记录用户拒绝、逐格现象与已确认技术原因；用户未授权重做、修改源码/资产或生成下一候选。

### 5.2 后续 W24 授权下的 W-C3 下一候选（隔离物化与机器回归完成）

5.1 的拒绝候选、用户原话和逐格表保持原文；本节另立后续授权候选。独立 compiler version 为
`capability-blank-4-timing-area-visual-execution`，Scene 状态根为
`W_C3_NEXT_CANDIDATE_VISUAL_PENDING`，当前状态仅为 `next_candidate / VISUAL_PENDING`。

源码新增专用 `TimingAreaCapabilityVisualExecutor`，把十项 Trace 接到真实 Renderer、LineRenderer、Transform、MaterialPropertyBlock 与一个固定容量手动 ParticleSystem：预警形状/填充与爆发槽、延爆加速闪烁、逐 Tick 槽载体、三级蓄力与满级提示、Channel 双尾段、五个独立连锁位置、扩张边缘命中层、收缩/屏息/聚爆、外部局部路径与残留、三级密度与升级脉冲均有确定性公开读回，并由测试与真实粒子/Renderer 状态交叉验证。charge、channel、moving-zone 的 complete/cancel 尾段由 Runtime Entry 实现；Preview Driver 只发外部路径和生命周期输入。

当前固定结构为每 Entry `Renderer =5 / PS =1 / 粒子容量 ≤32 / 材质引用 ≤2`，无逐帧 Instantiate。有槽模式由 Compiler 解析支撑 Recipe 的首个启用正式模板，并把 render mode、可选 mesh 与 material/texture 绑定固化到 `ResolvedVisualSlotBatch_*`；无槽模式使用 neutral carrier，不以字符串冒充槽执行。Stop/Reset 必须清空真实载体、尾段和读回。Preview 使用 4 列 × 3 行有界墙，Entry scale `0.16`，最大半径/路径 `4` 的包络留在单格内，状态文字只声明下一候选与视觉待签；Preview Driver 已由生产规则禁止进入正式 Runtime Entry。

隔离 shadow 已串行物化并验证：executeMethod
`VFXComposer.Editor.Capabilities.CapabilityBlankCompiler.BuildTimingAreaBlanks` exit `0`；
`TimingAreaCapabilityTests` `6/6`；`TimingAreaCapabilityRuntimeTests` 修复后 `5/5`；W-C3-only
Preview `1/1`。随后共享回归通过 W-C1 Edit/Play/Preview `7/7 + 2/2 + 1/1`，以及 W-C2
`5/5 + 9/9 + 1/1`。完整 XML 位于 isolated shadow 的 `test-results/wc3-*.xml` 与
`test-results/wc3-regression-*.xml`。这些结果不含 Beauty-frame 判断，不授予用户视觉、L3 或 L4。

## 6. 预览与视觉边界

已生成三个计划内预览场景：

- `Assets/VFX/Preview/VFXPREVIEW_CapProjectile.unity`
- `Assets/VFX/Preview/VFXPREVIEW_CapBeam.unity`
- `Assets/VFX/Preview/VFXPREVIEW_CapTiming.unity`

场景构建测试只证明：场景可重建、包含计划数量的稳定 Cell、引用正式 Runtime Entry、没有临时 Recipe/Generated 残留。它**不证明视觉质量通过**。W-C1、W-C2、W-C3 的旧候选均已于 2026-08-25 被用户视觉拒绝；“当时未授权重做”保留为历史事实。其后 W24 全项目授权另立的新候选均保持 `VISUAL_PENDING`，不能回写旧拒绝。

W-C0–W-C3 阶段当时只完成 `style` 的 Schema/解析/校验预留；W1 完成后已追加三份正式成品示范：`cap_demo_fan_wave_cartoon_2d`、`cap_demo_charge_occlude_holo_3d`、`cap_demo_telegraph_nova_holy_3d`。三者均由 Styled Runtime Entry 执行真实 CapabilityTrace，不是给中性素体简单换色。

## 7. 实测验证

最终串行执行（同一 Unity 项目没有并发 Batch 进程）：

| 门禁 | 结果 | 证据 |
|---|---:|---|
| Unity compile-check | exit 0 | `test-results/unity-compile.log` |
| W-C0 infrastructure | 7/7 | `test-results/wc0-capability-infrastructure-v3.xml` |
| W-C0 Domain regression | 24/24 | `test-results/wc0-domain-regression.xml` |
| Error-code bidirectional audit | 1/1 | `test-results/wc0-error-code-audit-v2.xml` |
| W-C1 EditMode | 7/7 | `test-results/wc1-projectile-full.xml` |
| W-C1 PlayMode | 1/1 | `test-results/wc1-projectile-play.xml` |
| W-C2 EditMode | 5/5 | `test-results/wc2-beam-capabilities.xml` |
| W-C2 PlayMode | 1/1 | `test-results/wc2-beam-play.xml` |
| W-C3 EditMode | 5/5 | `test-results/wc3-timing-area.xml` |
| W-C3 PlayMode | 1/1 | `test-results/wc3-timing-area-play.xml` |
| Preview Scenes | 1/1 | `test-results/capability-preview-scenes-v2.xml` |
| W-C3 next-candidate EditMode | 6/6 | `test-results/wc3-edit-current.xml` |
| W-C3 next-candidate PlayMode | 5/5 | `test-results/wc3-play-current-r2.xml` |
| W-C3 next-candidate Preview | 1/1 | `test-results/wc3-preview-current.xml` |
| W-C1 shared regression after W-C3 | 7/7 Edit + 2/2 Play + 1/1 Preview | `test-results/wc3-regression-wc1-*.xml` |
| W-C2 shared regression after W-C3 | 5/5 Edit + 9/9 Play + 1/1 Preview | `test-results/wc3-regression-wc2-*.xml` |
| **全量 EditMode** | **193 total / 158 passed / 0 failed / 35 historical Explicit skipped** | `test-results/capabilities-full-editmode-final2.xml` |
| **全量 PlayMode** | **22/22** | `test-results/capabilities-full-playmode-final.xml` |

35 个 Explicit 项是历史一次性证据/录制器生命周期测试；它们被显式跳过不等于本轮新增测试被跳过。本轮 W-C0–W-C3 门禁均为正常可运行测试。

## 8. 开发中暴露并修复的问题

1. 首次错误码审计暴露旧错误码文档漂移；补齐权威表后双向审计通过。
2. 首次弹道组合测试使用了没有 module 的伪 Recipe，被 Domain 必填规则先拦截；改为由完整合法正式 fixture 变异，避免测试夹具制造假故障。
3. 首次素体构建把 `scale=0.5` 写到正式 Manifest 最小值 `0.6` 以下；全部 Recipe 改为声明范围内值，而不是放宽 Manifest。
4. Preview Scene 初测依赖 Prefab YAML 内部名称；Prefab 实例序列化不保证该字段。改为对预览场景自己的稳定 `Cell_*` 根节点断言。
5. 全量回归发现旧保护测试把 Generated 写死为固定四目录；改为保护旧资产的 GUID/Hash/存在性，同时允许计划内新批次扩展。
6. 历史 S9/S11 隔离 Recipe 复制旧 fireball 层级，却被新 strict 规则当新成品审计。只为精确隔离 ID 登记 `legacy_audit`，产品能力 ID 继续默认 strict。
7. 旧 S12B 测试仍查找已归档 S14 authority run；更新为读取当前唯一 S15 真实 Update/同 MainCamera 证据，只验证证据链和共同锚点，不代签视觉质量。
8. W-C3 next-candidate PlayMode 首轮 `4/5` 暴露通用 smoke 把 `expand_ring` 的合法 `t=0` 零半径误当成至少两个 Renderer 的失败条件。修复只收窄通用首帧可见性断言；专用测试仍在非零时刻强制半径、edge layer 与 12 个真实载体。重跑为 `5/5`，没有通过伪造初始半径放宽产品语义。

上述可复用经验已递归写入 `docs/rules/60_ENGINEERING_LESSONS.md`。

## 9. 退出条件

W-C0–W-C3 的**机器能力开发退出条件已满足**。仍然开放的只有：

- 三份“能力+皮肤”示范的用户视觉签署（实现与机器行为门禁已完成）；
- W-C1、W-C2、W-C3 的旧 Preview 候选均已被用户视觉拒绝；后续 W24 授权已另立下一候选，三者均已完成隔离机器物化与定向/共享回归，但都没有产生新的用户视觉结论。

后续开发顺序回到 `docs/allwork/00_INDEX_AND_ACCEPTANCE.md` 顺位 3；不得因本报告把任何视觉项改写为“用户已通过”。
