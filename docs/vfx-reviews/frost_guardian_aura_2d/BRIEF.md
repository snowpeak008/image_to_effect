# Frost Guardian Aura 2D — Gate 0 Brief

状态：`rejected at visual direction — user requested a more complex fire effect`  
EffectId：`frost_guardian_aura_2d`  
Archetype：`aura`  
Dimension：`2d`  
Lifecycle：`looping`  
Topology：`area`（跟随宿主，不承担 Area gameplay 判定）  
Attachment：`caster`  
Target Profile：`pc_editor`，同时保留 `mobile_medium` 预算检查  

## 1. 视觉目标

角色脚下持续存在的冰霜守护光环。中心约 `35%` 半径保持安静，为角色和地面信息留出空间；外层为宽厚、半透明、连续闭合的冰霜环体，内层为较细的青白能量流。`6–8` 枚小冰晶沿切线方向缓慢顺时针环绕，冷雾与少量雪粒形成稳定呼吸，不表现为一次性命中爆炸。

目标参考：[reference/target-v1.jpg](reference/target-v1.jpg)  
SHA-256：`8F2BC388E4695E5E3AB69BCC0AF3C4CE5E58A3946C735164ACDF4C0AADC377A2`  
说明：参考图由内置 ImageGen 生成，只用于视觉方向、层级和模块拆解；已压缩为 `194,264 B` 的文档证据，不进入 Unity `Assets`、Player 或 Runtime 递归依赖。

## 2. 明确不做

- 不做中央爆点、放射尖刺或一次性 Impact。
- 不把完整参考图、完整冰环截图或雪花截图作为 Runtime Sprite。
- 不依赖 Bloom 才能读清轮廓。
- 不允许环退化为单线、规则齿轮、重复图章或出现首尾暗缝。
- 不让环绕冰晶统一指向圆心；它们沿轨道切线移动。
- 本轮不实现伤害区域、减速逻辑或 Buff 数值，只提供可供游戏逻辑调用的 Aura 视觉入口。

## 3. 生命周期与动作合同

| 状态 | 时间/规则 | 视觉动作 |
|---|---|---|
| Start | `0.00–0.30s` | 外环由低 Alpha 和较小尺度平滑建立；冰晶分批进入，不同时闪现 |
| Active | `2.40s` 无缝循环 | 外环缓慢呼吸；内环与雾层错相旋转；冰晶稳定环绕，循环边界不可见 |
| Refresh | 不重新 Start | 重置逻辑持续时间，但不重置粒子、轨道角度或制造亮度跳变 |
| Stop | `0.30s` AllowTail | 停止新生粒子，环体和已有冰晶自然淡出 |
| Force Stop | 当帧 | 清空并回池，用于宿主销毁和场景卸载 |

跟随策略：跟随宿主世界位置与批准的统一缩放；默认不继承宿主瞬时旋转，避免地面 Aura 随角色朝向抖动。宿主瞬移时同帧跟随，不产生世界空间拖尾。挂点丢失时安全 Stop。

叠加策略：相同 EffectId 再次施加执行 Refresh，不复制第二实例；不同 Tier 由高 Tier 替换低 Tier；不同 Aura 可共存，但需要独立排序和总预算审计。

## 4. Module Decomposition Table

| 视觉角色 | 实现候选 | 运行节点 | 复用/来源 |
|---|---|---:|---|
| Outer Frost Body | 闭环合并 Mesh + 周期 Frost Shader | 1 | 复用 Frost Ring Shader 族；为 Aura 使用连续轮廓参数 |
| Inner Energy Weave | 闭环 Mesh + 错相周期 Noise | 1 | Frost Aura/Area 可共享 |
| Orbiting Crystals | 1 ParticleSystem 或单 Renderer 批量实例 | 1 | 复用 `T_Frost_ShardAtlas_A_v1`，不复制纹理 |
| Cold Mist | 低频环形 ParticleSystem | 1 | 复用 Frost Mist Shader/参数族 |
| Snow Motes | 稀疏 ParticleSystem | 1 | 程序化 Mote，共享 Frost Family |
| Runtime Controller | Aura Start/Refresh/Stop/Pool 状态机 | Root | 新增 Aura 通用 Runtime 能力 |

拆解结论：`segmented/procedural`。逻辑上允许分段生成 Mesh，但最终外观必须连续。完整概念图只作 Reference，不参与裁切或 Runtime 打包。

## 5. 结构与资源预算

- Runtime Entry：严格 `1` 个 Prefab。
- GameObject：目标 `6`，上限 `8`；最大深度 `1`。
- Renderer：目标 `4`，上限 `6`。
- ParticleSystem：目标 `2`，上限 `3`。
- Local Shader/Texture：`0`；优先只引用版本化 Frost Shared 资产。
- Local Material：目标 `0`，如 Render State 无法共享，上限 `2` 并记录理由。
- 运行时 PNG：不得新增完整 Aura 图；预计只复用现有 `22,527 B` Shard Atlas。
- Active 稳定态峰值粒子：`<= 24`；不随循环时长持续增长。
- Generated 目录只允许最终 Runtime Entry 及获批 Data Asset，不保留截图、旧版本或内置 Manifest。

## 6. 适用经验库检查

| 经验 | 本轮约束 |
|---|---|
| `EXP-001` | 工程通过与用户视觉通过分开记录 |
| `EXP-002` | 资源优化必须同相机、同时间线 A/B，不得再次把环优化成单线 |
| `EXP-003` | 逻辑拆分不得显示为规则分段 |
| `EXP-004` | 外环和内环同时保证几何、着色和验收闭合 |
| `EXP-005` | Aura Compiler 行为进入版本或 Build Hash |
| `EXP-006` | 共享 Material 改 Shader 后审计旧纹理 GUID 不可达 |
| `EXP-007` | Source/Build/GPU 分栏报告，Shared 仍计完整驻留 |
| `EXP-008` | 同一保存场景、同一序列化 Camera、自然 Update 录制 |
| `EXP-009` | 无缝循环、粒子不增长、Stop 清空和旧 GUID 不可达形成回归测试 |

## 7. Gate 1 视觉方向验收

用户只需先判断目标参考是否满足：

1. 是角色脚下持续守护 Aura，而不是 Impact。
2. 中心留空足够，不遮挡角色。
3. 外环宽厚、有冰霜体积且连续闭合。
4. 内环、雾、冰晶和雪粒层级清楚，但不抢角色主体。
5. 整体冷蓝、稳定、克制，不靠过曝 Bloom。

用户确认前不进入正式 Recipe、Compiler、Prefab 和完整回归；若拒绝，只修改目标 Brief/参考，不制造待清理的 Unity 资产。

## 8. 视觉方向结论

用户判定该方向与此前效果都过于简单，要求改做视觉层次更复杂、但运行时 PNG 极简且体积受控的火焰特效。本候选未进入 Unity `Assets`、Recipe、Compiler 或 Generated，因此无需清理运行时资产；后续由 `inferno_vortex_area_2d` 继续 Gate 0/1。
