# W15 新增 Archetype 实施报告

状态：**源码与机器门禁完成；用户视觉拒绝（2026-08-25）**  
日期：2026-08-25  
权限边界：用户已拒绝当前视觉候选；机器门禁不折算为视觉通过。本次签署未授权重做、修改源码/资产或生成下一候选。

## 1. 类型扩容

Recipe v1-compatible 1.2 契约新增 6 个权威 Archetype token：

- `decal`
- `weapon_trail`
- `destruction`
- `lifecycle`
- `portal`
- `loot`

`docs/rules/10_ARCHETYPE_PROFILES.md` 已在内容合入前登记各自 MUST/SHOULD、生命周期、建议层级和预算。`ArchetypeParameterRegistry` 对每类 3 个必填语义参数执行精确字段、JSON 类型、枚举和范围校验；稳定错误码为 E1810–E1812。AI-readable Schema 同步允许这些 token 与 `archetypeParameters`，权威仍是 C# Parser/Registry。

## 2. 正式内容与协议

| Archetype | 正式内容 | 运行时协议 |
|---|---|---|
| Decal | `scorch_decal_3d`, `frost_decal_3d` | surface key + point/normal、法线 bias、oldest-first stack limit |
| WeaponTrail | `katana_trail_weapon_3d`, `energy_whip_trail_2d` | 外部 blade root/tip、速度阈值、8–16 历史点、停顿淡出 |
| Destruction | `crate_break_destruction_3d`, `crystal_shatter_destruction_3d` | seed/索引确定性初速、重力/视觉反弹、外部 impulse；无 Rigidbody |
| LifeCycle | `death_dissolve_lifecycle_3d`, `hero_entrance_lifecycle_3d` | 外部 Renderer 绑定、MPB `_Dissolve`/燃边、Stop 不禁用 Gameplay Renderer |
| Portal | `twin_portal_3d` | 同 Recipe 双实例 `pair_id + entry/exit role`、外部 traverse 事件 |
| Loot | `loot_beam_pickup_3d` | rarity 1–5 共享材质调色、外部 pickup endpoint、到达后精确吸附并 Stop |

10 个输出均为 strict Runtime Entry，局部纹理为 0，`ownedOutputs[]` 只含自身 Prefab，共享 Shader/Material/Mesh/粒子 Mesh 均在 `dependencies[]`。每个内容另有一个 `set_archetype_param` 裸数组 Patch 示例；真实 Loot 临时事务验证确认 revision/history/重建后的 controller 参数一致并完成清理。

批次预览：`Assets/VFX/Preview/VFXPREVIEW_NewArchetypes.unity`。它有 10 个稳定 Cell、1 台序列化 Camera；Portal cell 同屏 entry/exit 两角色，Loot cell 同屏 1–5 五档。`NewArchetypePreviewDriver` 只存在于 Preview Scene，并已加入生产禁用组件表。

## 3. 机器验证

| 门禁 | 结果 | 证据 |
|---|---:|---|
| Compile | exit 0 | `test-results/unity-compile.log` |
| W15 EditMode | 4/4 pass | `test-results/w15-new-archetypes-edit-v3.xml` |
| W15 PlayMode | 3/3 pass | `test-results/w15-new-archetypes-play-v2.xml` |
| 全量 EditMode | 202 total / 167 pass / 0 fail / 35 historical Explicit skipped | `test-results/w15-full-editmode-v2.xml` |
| 全量 PlayMode | 27 total / 21 pass / 0 fail / 6 graphics evidence Explicit skipped | `test-results/w15-full-playmode.xml` |

## 4. 整改记录

1. 初版 Katana 使用仅支持 2D 的 `inkwash`，被 E1802 正确拦截；改为支持 3D 的 `semireal`，未放宽风格支持矩阵。
2. 共享依赖测试误写 `Shared/Style/`，实际正式目录为 `Shared/Styles/`；修正测试，不改资产布局。
3. Loot 首版在到达容差内 Stop，位置停在 0.142 而目标为 0.15；修复为 Stop 前精确吸附目标。
4. Patch 首版以 `style != null` 分派 Styled Compiler，误捕获旧 formal Recipe；改为按新 Archetype、受管路径或 Manifest compilerVersion 分派，旧 Patch/故障注入/Prefab 大小写全量回归恢复。

## 5. 用户最终视觉签署

Scene：`Assets/VFX/Preview/VFXPREVIEW_NewArchetypes.unity`  
结论：**拒绝**  
签署人：用户  
日期：2026-08-25  

用户原话：

> 拒绝；W15仅有六类Archetype的概念轮廓，Decal缺少三表面贴附，WeaponTrail缺少快慢挥差异，Destruction缺少完整破碎表现，LifeCycle未绑定角色溶解，Portal缺少出入口时序差异，Loot五档主要只换颜色；设计与实现不同步，整体未达到商用级视觉完成度。

| 条目 / 时间阶段 | 原计划视觉判断 | 当前画面与技术核对 |
|---|---|---|
| 格 1–2 Decal；每轮约 `0.08s` 触发 | 地面、墙面、45° 三种表面角度贴附且无穿插 | 用户判定缺少三表面贴附。Preview 只有两个 Decal 条目，驱动仅依次提供正面与 45°法线，且没有可辨识的三类承载表面，不能形成原计划证据 |
| 格 3–4 WeaponTrail；全轮持续驱动 | 双端点跟随，快挥亮、慢挥在阈值下淡出，历史采样平滑 | 用户判定缺少快慢挥差异。Preview 对两条 Trail 均使用固定角速度连续挥动，没有慢挥/停顿阶段，因而不能验收速度阈值与淡出差异 |
| 格 5–6 Destruction；约 `0.08s` 触发，约 `1.25s / 1.70s` 结束 | 木箱 8–12 块碎裂与尘土衔接；水晶碎裂、折光和悬浮微尘；运动含确定性重力与两次反弹衰减 | 用户判定缺少完整破碎表现。当前主要呈现短暂、零散的碎片运动，未建立清楚的原物体—爆散—反弹—尘土/微尘视觉过程 |
| 格 7–8 LifeCycle；约 `0.08s` 触发，约 `1.40s / 1.25s` 结束 | 外部角色 Renderer 上的死亡溶解与反向聚合登场，并显示燃边、灰烬及落地反馈 | 用户判定未绑定角色溶解。Preview 未调用 `BindExternalRenderers`，没有角色 Renderer 作为验收对象，当前内部轮廓不能证明身体溶解或聚合 |
| 格 9 Portal；约 `0.08s` 两端同时触发 | 入口吸入 `0.2s`，随后出口端延迟 `0.15s` 吐出波；entry/exit 角色清楚可辨 | 用户判定缺少出入口时序差异。Preview 同时触发两个实例；`entry/exit` role 已配置，但当前 Controller 没有按 role 分支视觉时序，画面主要是对称双环 |
| 格 10 Loot；约 `0.08s` 播放，约 `2.15s` 开始拾取 | 稀有度 1–5 在颜色、光柱高度/粗细、环数、星闪率上分级；拾取时收束为弧线飞向外部端点 | 用户判定五档主要只换颜色。当前 `SetRarity` 只改变颜色与强度，五实例同尺度；拾取阶段以 `MoveTowards` 直线移动整个条目，没有原计划的收束飞收弧线 |

本次“未达到商用级”只记录为用户对视觉制作完成度的评价，不作版权、许可或法律解释。当前候选状态为 `rejected`；在用户另行授权前，不进入重做、不修改 Unity Scene/Prefab/源码，也不生成下一候选。
