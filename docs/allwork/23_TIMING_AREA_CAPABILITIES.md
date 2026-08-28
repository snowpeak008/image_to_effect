# W-C3 时序 / 范围 / 施法能力批次（10 个能力素体）

> 执行状态（2026-08-25）：**10 个素体的机器开发与既有门禁仍为完成；用户已对 `VFXPREVIEW_CapTiming.unity` 作出最终视觉结论：拒绝。** 当前候选为 `rejected`，未授权重做、修改源码/资产或生成下一候选。正式示范 `cap_demo_telegraph_nova_holy_3d` 使用 `telegraph + expand_ring + holy palette`。

> 后续授权边界：上行是旧候选被拒绝时的历史状态，原结论不改写。用户随后授权按 W24 继续全项目开发，因此本节末尾另立下一候选；其状态保持 `next_candidate / VISUAL_PENDING`，等待最终集中视觉验收。

> 目标：补齐时序结构与空间形态两个能力域。这些能力决定"特效什么时候、在哪里、以什么节奏生效"——预警后爆发、延迟引爆、周期 Tick、蓄力分级、引导打断、扩张收缩、移动区域，是技能系统对特效层最常提的需求。
> 批次预览场景：`VFXPREVIEW_CapTiming.unity`。

## 1. 时序能力（timing.*）

| token | 素体 id | 行为定义 | 关键参数 | 断言要点 |
|---|---|---|---|---|
| `telegraph` | cap_telegraph_impact_3d | 预警→爆发：地面预警形状（圆/扇/矩形/环，与 warning_telegraph_3d 归并登记）充能进度可读，到时爆发 | shape、warn_duration、fill_style(边缘收拢/中心填充) | 爆发时刻=warn 结束帧；预警进度与时间线性 |
| `delay_fuse` | cap_delayfuse_impact_2d | 延迟引爆：载体到位后闪烁倒计时再爆（粘弹/定时雷） | fuse_time、blink_accelerate | 闪烁频率递增曲线、引爆时刻 |
| `tick_pulse` | cap_tickpulse_area_2d | 周期 Tick：区域按节拍脉冲（DoT/治疗场标准件，归并 static_field 等的 tick 语义） | tick_interval、tick_visual_slot | Tick 事件等间隔、槽引用生效 |
| `charge_release` | cap_charge_release_2d | 蓄力分级释放：按住蓄力 1–3 级（视觉逐级增强），松开按当前级释放不同规模；与 22 号 charge_scale 共享分级协议 | level_thresholds、per_level_scale、overcharge(满级溢出提示) | 级别切换事件时刻、取消/释放两路径干净 |
| `channel_interrupt` | cap_channel_3d | 引导可打断：引导期持续表现+进度环，被打断时**溃散演出**（区别于正常结束的收束演出——两种结束是两个必需出口） | channel_time、interrupt_scatter_scale | 完成/打断两出口事件与视觉分支均触发 |
| `chain_sequence` | cap_chainseq_impact_2d | 连锁序列：N 个爆点按拓扑（线/环/收拢）与间隔接力（归并 chain_blast 语义为通用能力） | count、interval、topology | 各爆点时刻与位置阵型 |

## 2. 空间形态能力（area.*）

| token | 素体 id | 行为定义 | 关键参数 | 断言要点 |
|---|---|---|---|---|
| `expand_ring` | cap_expand_area_3d | 扩张环/新星：作用边界从中心外扩，边界经过处触发命中（登记 nova 类语义） | max_radius、expand_speed、edge_thickness | 边界半径-时间曲线、on_hit 随边界到达 |
| `implode` | cap_implode_area_3d | 收缩聚爆：外环收拢至中心后引爆（吸聚感与 expand 相反，消散前必有 0.1s 屏息帧） | start_radius、collapse_time | 收拢曲线、聚爆时刻、屏息帧存在 |
| `moving_zone` | cap_movingzone_area_3d | 移动区域：区域整体沿外部路径移动（毒圈/火墙推进），边界形变平滑，路径残留可选 | follow_lag、residue_slot | 区域中心滞后曲线、残留寿命 |
| `growth_stage` | cap_growth_area_2d | 阶段成长区域：区域分 2–3 阶段升级（小→中→大，每阶段边界与内部密度跃迁），技能升级/叠层场景标准件 | stage_count、per_stage_radius、upgrade_pulse | 阶段跃迁事件、各阶段半径 |

## 3. 通用协议（本批次基础设施）

1. **视觉槽（slot）机制定版**：`tick_visual_slot / residue_slot / impact_slot`（22 号引入）统一为「能力引用其他 Recipe 作为节拍/残留/命中视觉」的组合机制，仅一层嵌套，构建期递归依赖报告。这是"能力搭积木"的关键件。
2. **双出口协议**：所有可中断能力（channel、charge、moving_zone）必须实现 `Complete` 与 `Cancel` 两个视觉出口，Runtime 方法与事件登记进 API 文档——只有正常结束出口的能力视为未完成。
3. **进度可读性**：telegraph/charge/channel 的进度表现必须与实际时间线性或按声明曲线对应（机器采样验证），杜绝"预警条走完了还没爆"。

## 4. 素体视觉规格与验收

- 素体统一中性色三层（区域面/边界线/事件标记）；预算：粒子 ≤32 / PS ≤2 / 材质 ≤3 / Renderer ≤5。
- 10 素体采样断言+确定性复跑；双出口全覆盖演示；槽机制组合冒烟 2 例（`telegraph+impact_slot`、`tick_pulse+tick_visual_slot` 引用素体六角闪）。
- 归并项（warning_telegraph、chain_blast、static_field tick）完成 token 登记与采样补验，原资产不重做。
- 套皮示范 1 例：`telegraph+expand_ring` 组合套圣光皮肤出 `holy_nova_telegraph_3d.json`——一个 Recipe 同时引用两个能力 token，证明能力可叠加成完整技能表现。

### 4.1 用户视觉拒绝记录（2026-08-25）

用户结论：**拒绝**。用户原话：“拒绝；W-C3仅完成逻辑采样，10格同时播放且跨格重叠、裁切严重；各格共用同色圆环，缺少预警爆发、加速闪烁、Tick视觉、三级蓄力、双出口、独立连锁爆点、聚爆、残留和升级脉冲等原计划视觉表达。”

| 格 | 预期 | 实际 |
|---|---|---|
| 1 `telegraph` | 预警形状/填充、线性进度与同帧爆发 | 只有中心球、放大环和通用小环，无填充/形状差异/真正爆发 |
| 2 `delay_fuse` | 加速闪烁后引爆 | Width 数值未进入 Area 渲染，无加速闪烁 |
| 3 `tick_pulse` | 五次等间隔 Tick 均调用视觉槽 | `on_tick` 不可见，槽未实例化，只有周期缩放环 |
| 4 `charge_release` | 三级增强、满级提示、释放/取消 | 无三级跃迁、满级提示、可见释放或取消 |
| 5 `channel_interrupt` | 正常收束与打断溃散两个出口 | 只有进度圆环，两个出口最终均立即隐藏 |
| 6 `chain_sequence` | 五个独立爆点按直线接力 | 同一个通用 Marker 跳动并跨格，无独立爆点槽视觉 |
| 7 `expand_ring` | 连续外扩及沿边界的命中层 | 巨大单色环覆盖多格，仅有一次通用 Marker |
| 8 `implode` | 收缩、屏息、中心聚爆 | 屏息不可见、归零回跳，只有通用小环闪 |
| 9 `moving_zone` | 外部路径、平滑边界、残留与双出口 | 趋近固定 Target，无外部路径、残留或分支出口，并进入格 10 |
| 10 `growth_stage` | 三级边界/内部密度跃迁与升级脉冲 | 只有同色环跳尺寸，无密度变化或脉冲，并跨格/裁切 |

整体 Scene 还存在结构性问题：10 格同帧播放，格距小于多个环的直径，所有实例相对标签左移，外侧条目落出相机；统一灰蓝加法材质与同款球/环/Marker 在重叠后无法辨认归属。机器测试只验证纯采样数据、事件和 Renderer/Cell 数量，不构成视觉通过。

根据用户指令，本次只记录拒绝、直接现象与已确认技术原因。下一候选的允许修改范围当前为“无”；须等待用户另行授权，不自动重做或扩大到 W-C1、W-C2。

### 4.2 W24 后续授权下的下一候选（隔离物化与机器回归完成）

旧候选、用户原话及上表保持不变。下一候选使用独立 compiler version
`capability-blank-4-timing-area-visual-execution`，并以场景状态根
`W_C3_NEXT_CANDIDATE_VISUAL_PENDING` 与旧候选隔离。该状态只表示下一候选的机器实现已物化，绝不覆盖旧拒绝或授予视觉通过。

下一候选把十项 Trace 接入专用 `TimingAreaCapabilityVisualExecutor`：

- `telegraph` 读取线性进度、形状与填充，并在结束帧执行真实 Impact 批量载体；
- `delay_fuse` 把递增加速频率接到可见闪烁，并在 fuse 结束执行爆发载体；
- `tick_pulse` 的每次 Tick、`chain_sequence` 的五个独立位置都进入一个固定容量手动 ParticleSystem，公开执行次数、序号、位置及真实粒子交叉读回；
- `charge_release` 提供三级尺寸/密度、满级提示及按当前级别释放和取消两种尾段；`channel_interrupt` 提供正常收束与打断溃散两种尾段；
- `expand_ring` 具有连续边界和沿边命中粒子层；`implode` 区分收缩、0.1 秒屏息与中心爆发，零半径不再回跳；
- `moving_zone` 接受外部局部路径，输出区域中心、路径残留及两种尾段；`growth_stage` 读取三阶段半径、内部密度和升级脉冲。

每个 Runtime Entry 固定为 `Renderer =5 / PS =1 / 粒子容量 ≤32 / 材质引用 ≤2`，无逐帧实例化；有槽模式在编译期把支撑 Recipe 首个启用的正式模板解析并固化其 render mode、可选 mesh 与 material/texture 绑定，无槽模式使用 neutral carrier。Immediate Stop 与 Reset 必须清空所有真实载体。Preview 改为 4 列 × 3 行有界墙，Entry scale 为 `0.16`，半径/路径最大值 `4` 的包络留在单格内；Preview Driver 只提供外部路径与 charge/channel/moving-zone 的 complete/cancel 循环输入，不手写正式表现，并已被生产规则列为禁用组件。

隔离 shadow 已串行执行 executeMethod
`VFXComposer.Editor.Capabilities.CapabilityBlankCompiler.BuildTimingAreaBlanks`（exit `0`），随后
`TimingAreaCapabilityTests` `6/6`、`TimingAreaCapabilityRuntimeTests` `5/5`、W-C3-only Preview
`1/1`。共享回归同时通过 W-C1 Edit/Play/Preview `7/7 + 2/2 + 1/1` 与 W-C2
`5/5 + 9/9 + 1/1`。PlayMode 首轮曾为 `4/5`：`expand_ring` 在合法的 `t=0`
零半径帧只启用一个 Renderer，而通用 smoke 错误要求至少两个；修复仅把通用首帧断言改为
“至少一个真实 Renderer 且非全隐藏”，专用测试仍在 `t=.72` 强制半径、edge layer 与 12 个
真实载体，未伪造初始半径也未削弱行为合同。机器门禁只关闭执行层、预算、清理和共享回归；
最终视觉结论仍由用户集中作出。
