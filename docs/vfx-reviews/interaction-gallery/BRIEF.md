# 运行时交互与组合能力九宫格 Brief

日期：2026-08-24  
状态：冻结后实施

## 验证目标

本轮不新增顶层 Archetype，而是使用已有 Projectile、Aura、Area、Beam、Trail、Transform 与 Composite 语义验证九种运行时能力。

| 格 | EffectId | 能力 | 必须看见 |
|---|---|---|---|
| 1 | `focus_charge_3d` | Charge | 核心聚能、轨道收束、释放脉冲 |
| 2 | `channel_tether_3d` | Channel | 持续双端点、主束和能量传输 |
| 3 | `warning_telegraph_3d` | Telegraph | 地面范围、倒计时填充、触发爆发 |
| 4 | `chain_arc_3d` | Chain | 多节点折线连接、动态端点 |
| 5 | `seeker_orb_3d` | Homing | 弯曲追踪路径、运动头、尾迹、目标 |
| 6 | `weapon_enchant_3d` | Bone/Weapon Attach | 武器主体、贴附能量、挥动残迹 |
| 7 | `phase_dash_3d` | Teleport/Dash | 起点残像、位移 streak、终点重组 |
| 8 | `dissolve_transform_3d` | Dissolve/Transform | 原形消解、过渡碎片、新形建立 |
| 9 | `ultimate_sequence_3d` | Multi-stage | 蓄力、飞行、命中、残留按阶段接力 |

## 禁止项

- 禁止九项共用同一个主轮廓后只换色。
- 禁止把 Gallery Driver、标签或 Cell 布局写入正式 Runtime Entry。
- 禁止用静态关键帧冒充状态切换；证据必须来自自然 Update。
- 禁止新增 PNG；本轮只使用程序化 Mesh、Line/Trail 和已有共享 Shader/Material。
- 禁止把组合技能合并成不可停止的单块动画；所有 Entry 必须支持 Stop/Reset/Pool。

## 验收顺序

先验收九宫格同步读形，再分别检查动态端点、挂点、重播、中断和清理。机器结果只证明工程约束，不替代用户视觉验收。
