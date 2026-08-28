# Frost Impact 2D 视觉失败记录与修复计划

日期：2026-08-23  
对象：`frost_impact_2d` Revision 2 / Compiler `impact2d-14`  
结论：**视觉验收失败；工程结构测试通过不构成视觉通过。**

## 1. 失败证据

- 用户峰值截图：核心、冰晶与光芒同时使用加法叠加，中心大面积裁成纯白，冰晶内部纹理丢失。
- 用户衰减截图：冰晶很快退成深蓝近黑，冰环仍以灰白色大面积停留，两个模块不像同一套冰系能量。
- 当前权威录制中的 `frame_007_peak.png` 与 `frame_015_decay.png` 能复现相同问题，因此不是 Game View、Gizmo 或用户观察方式造成。
- 目标图要求“短促白青核心 → 可读冰蓝晶体 → 从属破碎冰环与雾”，当前实现没有形成该亮度层级。

## 2. 根因

1. **素材动态范围未统一。** Ring、Mist、Shard 与 Core 分别生成和抠图，只统一了尺寸、Alpha 和 Atlas 布局，没有统一亮度、饱和度与边缘能量。
2. **混合模式被错误地一刀切。** 五个材质均使用 `Blend SrcAlpha One`；中心多层重叠必然过曝，白色 Ring 也会失去冰蓝色层次。
3. **生命周期被错误地一刀切。** Core、Shard、Ring、Mist、Mote 共用同一个 `Fade()`；这忽略了核心应短、晶体应保持、冰环应从属、雾应柔和的功能差异。
4. **视觉门禁缺失。** 现有自动化覆盖文件所有权、Atlas、Prefab、Manifest、相机与真实播放，却没有覆盖峰值过曝比例、模块亮度顺序和衰减一致性。
5. **验收顺序错误。** 在用户视觉签署前，报告使用了“工程候选完成”的表述，容易把技术闭环误认为可交付视觉。

## 3. 修复范围

本轮冻结 Recipe、Atlas 单元、Prefab 层级、相机和播放链路，只修复视觉能量：

- Core 与 Shard 保留受控 Additive；Ring 与 Mist 改为普通透明混合，避免长期灰白加法累积。
- 删除通用 `Fade()`，为 Core、Shard、Ring、Mist、Mote 分别定义颜色和 Alpha 曲线。
- 缩短并缩小 Core 的高亮阶段；Shard 延后峰值并在飞行中保持冰蓝纹理，到末段才快速淡出。
- Ring 使用更饱和的冰蓝色、中等 Alpha 和更早退场；Mist 只作为低亮度空间层。
- Compiler 版本升级以强制重建正式 Prefab，同时保持 Runtime Prefab GUID。

## 4. 固定亮度层级

以下是艺术门槛，不要求所有模块亮度相同：

| 模块 | 相对峰值 | 时间职责 |
|---|---:|---|
| Core | 1.00，仅数帧 | 提供命中焦点，不能吞掉晶体纹理 |
| Shard | 0.55–0.75 | 主体，飞行中持续可读且保持蓝青色 |
| Ring | 0.35–0.50 | 构图边界，从属于晶体，不得呈持久灰盘 |
| Mist | 0.12–0.25 | 柔和空间感，不与 Ring 争夺注意力 |
| Mote | 0.20–0.40 | 少量高频细节，不改变主体曝光 |

## 5. 重跑与退出门禁

1. Compile 通过。
2. Impact EditMode 定向测试通过，并验证材质混合模式和五条独立曲线。
3. Impact Runtime PlayMode 通过。
4. 使用 `VFXPREVIEW_Impact2D.unity` 内同一序列化相机，以正常 Update、单次 Play 重录关键帧；禁止手工 Emit、SetParticles、跳时采样和替换相机。
5. 人工检查 `0.05s / 0.116667s / 0.25s / 0.483333s`：中心不过度裁白、晶体中后段仍可读、Ring 为冰蓝且从属、Mist 可见但不抢主体、完成帧为空。
6. 用户未签署前，状态只能是 `pending visual acceptance`，不得写为视觉完成。

## 6. 防复发规则

- “模块可复用”只说明文件与结构合格，不说明组合后的视觉能量合格。
- 新特效必须先建立模块亮度预算，再选择 Blend 和生命周期；禁止所有视觉模块默认共用一条 Fade。
- 每个 Visual MVP 至少保留峰值和衰减关键帧对照；技术测试通过但关键帧失衡时，阶段结论必须为视觉失败。

## 7. 修复执行结果

修复候选：Compiler `impact2d-16`；Recipe Revision 保持 `2`；Runtime Prefab GUID 保持 `3120e8b907d830b44bd173bc26969daa`。

- Core、Shard、Mote 使用受控 Additive；Ring、Mist 使用 `SrcAlpha / OneMinusSrcAlpha`。
- 五个视觉职责使用独立 Color/Alpha over Lifetime；不再共用旧 `Fade()`。
- 同一 Preview Camera、正常 Update、单次 Play 的 60fps 重录已完成，权威证据仍为 `evidence/current-run/`。
- 机器观察值：Core 白色裁切占前景 `1.3799%`；组合峰值 `0.0960%`；峰值 P95 亮度 `148.980`，衰减帧降至 `71.520`；完成帧前景像素 `0`。
- 最终验证：Compile 通过；Impact EditMode `5/5`；Runtime PlayMode `1/1`；Visual Capture PlayMode `1/1`。

以上证明原始过曝/曲线断层已得到约束，但不代替用户视觉签署。当前状态仍为 `pending visual acceptance`。

## 8. Revision 3 拆解复跑

后续资源审查确认 Revision 2 的完整 512×512 Impact Atlas 同时承载 Ring、Mist、Core、Mote，虽然比单图重复更好，但仍不满足“可拆尽拆”的全项目规则。Revision 3 / `impact2d-21` 因此执行了第二次结构修复：

- 移除完整 Impact Atlas 的正式依赖，保留文件只用于视觉回滚。
- Ring 由 16 段确定性几何合并为单 Mesh/Renderer，段内冰脊由程序化 Shader 生成。
- Core、Mist、Mote 改为程序化；Shard 继续复用唯一 256×256 / 2×2 静态变体 Atlas。
- Runtime PNG 源文件从 `184,844 B` 降至 `22,527 B`；完整依赖驻留纹理从 `329,040 B` 降至 `66,216 B`。
- 机器门禁继续通过，但当前画面只作为低资源视觉候选，不把资源优化等同于视觉通过。

### 冰环体积修复

`impact2d-21` 为消除机械齿轮面，把环面基础 Alpha 降到 `0.025`、同时缩窄环带，结果只剩内侧发光线，用户正确判定质感明显下降。后续修复没有恢复完整冰环 PNG，而是保持16个逻辑扇区合并为一个 Mesh：共享边界位置以消除黑缝，使用全环连续 UV、程序化噪声晶纹、半透明冰体与独立亮脊。`impact2d-26` 恢复体积后，用户又发现3点钟方向的首尾闭合线；根因是末边界角度抖动、半径采样和线性噪声没有同时回绕。当前 `impact2d-29` 已将首尾内外顶点设为精确相等，并把纹理噪声改为圆周坐标采样；定向 EditMode `5/5`、Runtime `1/1`、真实图形视觉录制 `1/1`，最终仍待用户视觉签署。

## 9. 经验递归状态

本案例不再只作为 Frost 的局部复盘。可跨类型复用的结论已提升到 `docs/rules/60_ENGINEERING_LESSONS.md`：

- `EXP-001/EXP-002`：工程通过与视觉通过分离，资源优化必须保留视觉 A/B 基线；
- `EXP-003/EXP-004`：逻辑拆分不等于可见分段，周期资源必须几何与着色双重闭合；
- `EXP-005/EXP-006`：Compiler 行为变化必须触发重建，Shader 替换必须清理序列化残留依赖；
- `EXP-007/EXP-008/EXP-009`：大小分栏报告、真实 WYSIWYG 证据和已知失败最小防回归测试。

对应条款已同步回写 `25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md`、`30_ACCEPTANCE_AND_DELIVERY.md`、`50_MACHINE_ENFORCEMENT.md` 和 `DEVELOPMENT_PLAN.md`。后续 Ring、Aura、Area、Trail、Shield 与 Environment 的 Gate 0/5 必须主动执行这些检查，不能等待同类接缝、细线化或残留依赖再次出现。
