# W-C1 弹道能力批次（12 个能力素体）

> 执行状态（2026-08-25，下一候选）：**旧候选的用户拒绝记录永久保留；本轮 W24 全项目开发授权后，格 10–12 的视觉执行层机器候选已完成并在隔离 Unity 中物化，状态为 `next_candidate / visual_pending`。** 这不是视觉通过：新 Preview 尚未由用户观看并签署。

> 目标：把 Projectile 从"直线飞行一种"扩展为完整弹道能力库。全部按 20 号总纲交付素体 + 采样验收；元素皮肤留给后续批次。
> 批次预览场景：`VFXPREVIEW_CapProjectile.unity`（含静止靶/移动靶/墙面/地面四种交互物，均为预览驱动器）。

## 1. 运动学能力（motion.*）

| token | 素体 id | 行为定义 | 关键参数 | 采样断言要点 |
|---|---|---|---|---|
| `linear` | cap_linear_proj_3d | 匀速直线（基准，登记既有实现） | speed | 速度恒定、方向不变 |
| `accel` | cap_accel_proj_3d | 加速/减速弹（慢出手快命中，或反之） | init_speed、accel、max_speed | 速度曲线匹配、封顶生效 |
| `parabola` | cap_parabola_proj_3d | 抛物线（迫击炮/投掷），落点为输入 | apex_height、flight_time | 过顶点时刻、落点误差 ≤1% |
| `homing` | cap_homing_proj_3d | 追踪（登记 seeker_orb 实现），丢失目标策略 | turn_rate、max_speed、lose_target_mode(straight/expire) | 目标距离单调递减、角速度受限 |
| `wave` | cap_wave_proj_2d | 蛇形/正弦航迹（主方向直线+横向振荡） | amplitude、frequency | 横向偏移包络、主方向净速度 |
| `boomerang` | cap_boomerang_proj_3d | 回旋往返：去程→驻留→返程回收 | out_distance、hover_time、return_speed | 三段时刻、终点回到发射者动态位置 |
| `bounce` | cap_bounce_proj_3d | 弹跳弹：碰面反射，每跳衰减 | bounce_count、energy_damping | 反弹次数、每跳速度衰减率、入反角相等 |
| `orbit_then_strike` | cap_orbit_proj_3d | 环绕蓄势后突进（环绕施法者 N 圈再射出） | orbit_radius、orbit_turns、strike_speed | 环绕角度累计、突进触发时刻 |

## 2. 命中拓扑能力（hit.*）

| token | 素体 id | 行为定义 | 关键参数 | 采样断言要点 |
|---|---|---|---|---|
| `pierce` | cap_pierce_proj_3d | 贯穿：穿过目标继续飞，逐段衰减，每次命中出命中标记 | max_hits、damping_per_hit | on_hit(n) 次数、穿后速度/尺寸衰减 |
| `split` | cap_split_proj_2d | 分裂：命中或到程后分裂 N 个子弹（子弹是嵌套 behavior 引用，仅一层） | child_count、split_angle、trigger(hit/range) | 分裂时刻、子弹初始方向阵型 |
| `chain_hop` | cap_chainhop_proj_2d | 跳跃：命中后转向下一目标（登记 chain_arc 语义的投射版），每跳衰减 | hop_count、hop_range、damping | 跳数、每跳目标切换事件 |

## 3. 发射模式能力（emission.*）

| token | 素体 id | 行为定义 | 关键参数 |
|---|---|---|---|
| `fan` / `burst_stagger` / `ring` | cap_volley_proj_2d（一个素体演示三模式） | Recipe 显式声明 `volley_showcase` 三阶段：扇形散射 → 错峰连发 → 环形齐射；各阶段仍复用正式 emission 方向/时序语义 | fan_count、fan_spread_angle、burst_count、burst_stagger、ring_count、ring_radius、phase_duration |

## 4. 素体视觉规格（统一）

- 三层：核心体（菱形/球，中性色）+ 方向指示（短拖尾，始终指向速度反方向）+ 命中标记（六角小闪，出现在每个 on_hit 事件点，0.2s）。
- split 子弹核心体缩小 60%；boomerang 驻留段核心体自旋加速提示状态切换。
- 预算：每素体粒子 ≤24 / PS ≤2 / 材质 ≤2 / Renderer ≤4（素体必须轻，行为才看得清）。

## 5. 验收
- 12 素体全部通过采样断言 + 确定性复跑（同 seed 逐字节一致）。
- 组合冒烟：至少 3 个跨子块组合（如 `fan+wave`、`burst_stagger+homing`、`parabola+split`）采样正常且合法性表拦住 ≥2 个非法组合。
- 预览场景四靶交互演示各能力一遍并录像。
- 套皮示范 1 例：`homing` 素体套火系皮肤出 `firebolt_homing_projectile_3d.json`，证明"能力素体+皮肤=成品"链路（视觉验收即可，无需重跑行为验收）。

### 5.1 用户视觉拒绝记录（2026-08-25）

用户结论：**拒绝**。用户原话：“格10未显示5个子弹，格11未依次跳转4个目标，格12未显示扇形且缺少错峰/环形模式。”

| 格 | 时间阶段 | 预期 | 实际 |
|---|---|---|---|
| 10 `split` | 约 `1.08s` | 5 个可见子弹按 `80°` 阵型分裂 | 只有逻辑事件与单一 Marker，未显示 5 个子弹 |
| 11 `chain_hop` | 约 `0.78s` | 依次跳转 4 个目标，每跳反馈与衰减 | 4 事件同帧覆盖单一 Marker，没有分时跳转 |
| 12 `volley` | 约 `0.08s` | 5 发 `50°` 扇形，且展示错峰与环形齐射 | 只显示单枚直线 Core；未显示扇形，无错峰/环形展示入口 |

直接技术原因：多子弹/多目标数据只存在采样 Trace 事件中，编译生成的中性 Prefab 只有单一 Core 和单一 Marker，运行渲染层未实例化事件的子弹、方向或目标序列。因此现有机器测试只证明逻辑事件/确定性，不证明原计划视觉已实现。

根据当时用户指令，本节只记录拒绝、直接现象与已确认技术原因。当时“未授权重做”的状态是历史事实；它已被 2026-08-25 后续 W24 全项目开发的明确授权取代，但旧拒绝本身没有被撤销，也不能改写为通过。

### 5.2 下一候选机器协议（2026-08-25）

本轮只关闭格 10–12 的已知技术根因，不改 W-C2/W-C3，不代签视觉质量：

| 格 | 真实执行载体 | 确定性读回 | 预算 |
|---|---|---|---|
| 10 `split` | 在 Trace 的 `on_split` 时刻由一个手动批量 ParticleSystem 同时维持 5 个子核；母核退出，子核持续到本轮结束 | `VisibleCarrierCount=5`；每个 `GetCarrierDirection(i)` 逐项等于事件 `After`；`GetCarrierScale(i)=0.6`；首尾夹角 `80°` | 1 PS / 1 ParticleSystemRenderer / 5 粒子；Prefab 总 Renderer ≤4 |
| 11 `chain_hop` | Core 在 4 个确定性目标间分段插值，不再保持原直线；每个到达点独立显示 0.2s Marker，Core/Marker 按 damping 缩小 | `on_hit(1..4)` 时间严格递增；目标 Position 不同；每跳 `After.magnitude = Before.magnitude × (1-damping)`；`ProcessedChainHopCount` 与 Core Position 可读 | 不新增 PS/Renderer；沿用 Core + Trail + Marker |
| 12 `volley` | 同一个手动批量 ParticleSystem 按三个 Recipe 阶段执行：`0–0.66s fan`、`0.66–1.32s burst_stagger`、`1.32–1.98s ring` | fan 为 5 发/50°；burst 为 5 发/0.09s 严格错峰；ring 为 8 发/45° 等角；`ActiveShowcaseMode`、Phase、数量/位置/方向可读 | 任一阶段峰值 8 粒子；1 PS；Prefab 总 Renderer ≤4 |

`cap_volley_proj_2d.default.json` 已提升为 revision 2，`behavior.emission.type=volley_showcase`，并以 `showcase_fan / showcase_burst_stagger / showcase_ring` 三个顺序 Stage 显式绑定模式、参数和时长。Compiler 强制这三个 Stage 的 id、trigger 和 duration 与 `phase_duration` 一致；`compilerVersion=capability-blank-2-carrier-showcase` 使下一次正式构建的 Manifest 通过 `sourceRecipePath + recipeHash + recipeRevision + compilerVersion` 绑定该协议，不能由未声明的手写 Preview 演出替代。

Preview Builder 现在把每个 Runtime Entry 缩放为 `0.2`，按 `3.25 × 1.82` Cell 布局并下移标签，最长 8-unit 轨迹仍留在单格水平边界内；场景只显示一个明确状态标识：`W_C1_NEXT_CANDIDATE_VISUAL_PENDING`。该标识不得出现 `accepted`。

新增/加强的机器验收覆盖：split 的 5 子核、方向、60% 尺寸与清理；chain 的严格递增时刻、目标序列、衰减和 Core 逐跳位置；volley 三模式数量、50°、0.09s stagger、45° ring；Stop/Reset 清空；同 seed 视觉读回复跑；Prefab Renderer/PS/粒子预算；二次构建 Prefab/Manifest 字节幂等与 GUID 不变；Preview 候选状态和格内缩放。

在静态 Roslyn/JSON/协议检查之后，本轮仅在隔离 shadow Unity 中执行了 W-C1
物化与定向门禁：12 个 Projectile Runtime Entry 以
`capability-blank-2-carrier-showcase` 重建；`ProjectileCapabilityTests` `7/7`、W-C1-only
Preview 格内/状态测试 `1/1`、`ProjectileCapabilityRuntimeTests` `2/2` 均通过。主 Unity
未被启动、关闭或写入。当前准确结论是：**新执行载体与机器验收候选完成，用户视觉签署仍为 pending；不声明视觉通过。**
