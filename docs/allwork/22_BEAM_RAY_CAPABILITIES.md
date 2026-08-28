# W-C2 射线 / 光束能力批次（8 个能力素体）

> 执行状态（2026-08-25）：**旧候选的用户视觉结论仍为拒绝；后续 W24 授权下的新候选已完成机器开发与隔离 Unity 门禁，状态为 `next_candidate / VISUAL_PENDING`。** 该机器结论不撤销旧拒绝，也不构成 L3、L4 或用户视觉通过。正式示范 `cap_demo_charge_occlude_holo_3d` 使用 `charge_scale + occlude + holo`。

> 目标：把 Beam 从"两端点持续束"扩展为完整射线能力库。射线与子弹的本质差异——**命中即时性与持续占线**——是本批次的核心：hitscan 瞬发、持续伤害束、扫射、蓄力变粗、反射折线、遮挡截断，每一种都是不同的端点动力学与时序协议。
> 批次预览场景：`VFXPREVIEW_CapBeam.unity`（含可移动靶、反射墙、遮挡柱驱动器）。

## 1. 能力清单

| token | 素体 id | 行为定义 | 关键参数 | 采样/事件断言要点 |
|---|---|---|---|---|
| `hitscan` | cap_hitscan_beam_3d | 瞬发射线：扣发即整线出现（≤1 帧），线体 0.1–0.2s 内衰减消失，命中点即时标记 | max_range、linger、width | 出现帧=触发帧；命中事件与触发同帧 |
| `sustained` | cap_sustained_beam_3d | 持续束（登记既有 Beam 实现为基准）：Start/Stop、端点跟随 | 已有协议 | 登记+补采样验收，不重做 |
| `sweep` | cap_sweep_beam_3d | 扫射束：持续束的目标端点沿外部驱动路径扫动，命中对象随扫过切换 | sweep_speed_max、inertia(端点滞后) | 端点滞后曲线、扫过时 on_hit 目标切换序列 |
| `charge_scale` | cap_charge_beam_3d | 蓄力变化：蓄力 1–3 级决定束宽/亮度/终段爆点规模；蓄力中可取消 | charge_levels、per_level_width、cancel_refund | 各级宽度阶跃时刻、取消路径干净 |
| `reflect` | cap_reflect_beam_3d | 反射束：命中反射面按入反角折行，最多 N 段，逐段衰减 | max_segments、damping_per_bounce | 折点角度、段数、每段宽度衰减 |
| `occlude` | cap_occlude_beam_3d | 遮挡截断：束终点=第一个阻挡物，阻挡点出灼烧点效果；阻挡物移开束延伸 | probe_interval、burn_point | 终点跟随遮挡变化的响应帧数 ≤2 |
| `converge` | cap_converge_beam_3d | 多源汇聚：N 条子束从环布源点汇于一点，汇点随功率增长 | source_count 2–5、focus_growth | 各子束端点一致性、汇点规模曲线 |
| `arc_link` | cap_arclink_beam_2d | 链式跳线（登记 chain_arc_3d / channel_tether_3d 实现）：跳段拓扑与下垂/抖动参数统一进 behavior 块 | hop_count、sag、jitter | 登记+补采样验收 |

## 2. 端点动力学协议（本批次基础设施）

所有射线能力共用一套**端点协议**，写入 Manifest：

- `source` / `target` 两端点均可为：固定点、Transform 跟随、外部逐帧驱动三种模式。
- 端点变化时束体响应帧数 ≤2（拉伸不得导致纹理挤压——纹理按世界长度平铺，`tiling_per_meter` 参数，沿用覆盖九宫格 B 的"长度变化禁止拉伸"规则并协议化）。
- 命中端统一出「端点效果槽」：`impact_slot` 引用一个 Impact 类 Recipe（缺省素体六角闪），这是射线与命中特效的标准组合点。

## 3. 素体视觉规格（统一）

三层：束体（中性色带）+ 源点标记（小环）+ 命中/折点/汇点标记（六角闪）。反射素体每段亮度递减必须可见；蓄力素体三级宽度差 ≥1.6 倍逐级可辨。预算：粒子 ≤24 / PS ≤2 / 材质 ≤3 / Renderer ≤5（converge 放宽 Renderer ≤7）。

## 4. 验收
- 8 素体采样断言+确定性复跑；hitscan 的"同帧命中"与 occlude 的"响应 ≤2 帧"为硬门禁。
- 组合冒烟：`charge_scale+occlude`、`sweep+impact_slot(引用 cap 素体)` 两例。
- 套皮示范 1 例：`reflect` 素体套雷电皮肤出 `mirror_arc_beam_3d.json`（折点用电火花 impact_slot），证明能力+皮肤+组合槽三者协同。

### 4.1 用户视觉拒绝记录（2026-08-25）

用户结论：**拒绝**。用户原话：“拒绝；格1缺少衰减，格2缺少端点跟随，格3无扫射，格4仅变粗，格5无反射多段，格6无烧灼点和遮挡变化，格7无四源汇聚，格8无跳线。”

用户未另行给出具体时间点；下表时间阶段来自本次签署前按该 Scene 的 `4.2s` 复播周期所做的只读技术核对，不扩写为用户逐帧原话。

| 格 | 时间阶段 | 预期 | 实际 |
|---|---|---|---|
| 1 `hitscan` | 约 `0.08–0.23s` | 同帧整束与命中标记，随后衰减 | 整束与通用端点环出现，但无衰减并直接关闭 |
| 2 `sustained` | 约 `0.08–3.08s` | Start/Stop 与端点跟随 | 固定两点直线，无移动端点驱动 |
| 3 `sweep` | 约 `0.08–2.08s` | 路径扫动、惯性及命中切换 | 固定 Target 覆盖 Sweep 结果，画面无扫射 |
| 4 `charge_scale` | 约 `0.08–2.08s` | 束宽、亮度、终段爆点三级变化及取消 | 只有束宽变粗，无亮度、爆点或取消演示 |
| 5 `reflect` | 起点环约 `0.58 / 1.08 / 1.58s` | 多段折线、折点与逐段衰减 | 固定单段线；同一 Marker 在起点闪三次 |
| 6 `occlude` | 周期起始约 `0.08s` | 遮挡截断、烧灼点、移障后两帧内延伸 | 固定合成截断，无真实遮挡变化或烧灼点 |
| 7 `converge` | 周期起始约 `0.08s` | 四源子束汇聚及焦点增长 | 逻辑事件未实例化，只有单源单线 |
| 8 `arc_link` | 跳跃事件约 `0.78s` | 依次跳线并表现下垂/抖动 | 四事件同帧覆盖同一 Marker，无跳线 |

直接技术原因：Beam 中性 Prefab 只有单一两点 `LineRenderer` 与通用 Marker，正式 Scene 也没有计划所述的移动靶、反射墙、遮挡柱或独立端点驱动；逻辑 Trace 事件未接入多段/多实例视觉执行层。现有机器门禁只证明纯采样事件和基础两点直线生命周期，不构成视觉通过。

在该次拒绝发生时，执行者只获准记录拒绝、直接现象与已确认技术原因，下一候选的允许修改范围为“无”。随后用户已授权按 W24 全项目继续开发，因此 4.2 另建新候选；本段仍保留当时的权限事实。

### 4.2 后续 W24 授权下的下一候选（机器完成，视觉待签）

本节是后续 W24 全项目授权建立的**新候选**，不回写也不撤销 4.1 的拒绝记录。状态固定为 `next_candidate / VISUAL_PENDING`，预览状态根固定为 `W_C2_NEXT_CANDIDATE_VISUAL_PENDING`；不得出现 `ACCEPTED`，不得据机器读回提升到 L3/L4 或用户视觉通过。八个 Prefab、Manifest 与 Preview 已在隔离 shadow Unity 中物化；这些是机器验证产物，不是正式视觉签署证据。

八格均改为确定性 Runtime 载体，而非只记录 Trace 或只在 Preview 中动画：

- `hitscan`：触发帧显示完整束与命中端标记，`0.1–0.2s` 内 alpha/宽度衰减到零，并公开 fade、alpha、width 读回。
- `sustained`：公开 Start/Stop 与 Transform/显式坐标端点协议；Source/Target 跟随时实时重算长度和世界长度平铺。
- `sweep`：采样端点沿非零弧运动，Runtime 保留最大角速度与惯性读回，不再被 sustained 固定端点覆盖。
- `charge_scale`：三级宽度与亮度、完成爆点、取消出口分别进入真实 Renderer/端点标记状态，并公开等级/亮度/出口读回。
- `reflect`：由反射 Trace 的位置和方向驱动最多 N 条真实分段 LineRenderer；折点用一个有界手动粒子批次，逐段宽度/亮度衰减可读回。
- `occlude`：只接受显式 `BeamCapabilityObstacleProbe`；未配置时 fail-closed，首个 Collider 截断并显示 burn point，Preview 自持可移动阻挡物，移障响应上限两帧。
- `converge`：四个环布源点使用四条真实 LineRenderer 汇聚同一点，焦点随功率增长。
- `arc_link`：单条有界多点 LineRenderer 按严格递增 hop 时间依次显现，每段包含确定性 sag/jitter。

固定预算为：普通格 `3 Renderer / 0 PS`，`reflect` 为 `5 Renderer / 1 PS / ≤16 粒子`，`converge` 为 `6 Renderer / 0 PS`；所有格只引用同一个共享中性材质，运行期间不逐帧 Instantiate。Stop/Reset 清除全部线、粒子、标记、外部端点与遮挡读回。当前 Recipe 协议已包含所需参数，因此没有为本候选改写 Schema/Recipe。

隔离 Unity 已执行 `VFXComposer.Editor.Capabilities.CapabilityBlankCompiler.BuildBeamBlanks`，输出 Manifest 的 compiler version 均为 `capability-blank-3-beam-visual-execution`。定向门禁结果为：`BeamCapabilityTests` EditMode `5/5`、`BeamCapabilityRuntimeTests` PlayMode `9/9`、W-C2-only Preview EditMode `1/1`。首次 PlayMode 暴露并修复两处真实问题：反射载体曾误消费不可见的下一 bounce 方向，Transform 驱动 Collider 的 bounds 在 FixedUpdate 前可能陈旧；最终实现按 `N segments = N-1 bounce vertices` 截断事件，并在显式有界 probe 读取前同步 Transform。共享回归仍为 W-C1 EditMode `7/7`、PlayMode `2/2`、Preview `1/1`。上述数字只证明机器执行层与旧路径回归，不授予视觉结论。
