# 空间与呈现覆盖九宫格 Brief

日期：2026-08-23  
状态：Gate 0 冻结；未作视觉通过声明

## 1. 目的

第一面九宫格已覆盖九种主要 2D 技能行为。本面不再复制相似图标，而是补齐尚未统一验收的空间与呈现边界：七种 3D 技能形态、Environment/Weather、Screen/UI。通过后，`docs/rules/10_ARCHETYPE_PROFILES.md` 中列出的正式 Archetype 与呈现空间才算完成视觉纵切覆盖。

## 2. 九格定义

| 格 | 候选 | 生命周期 | 必须一眼可见的职责 | 禁止项 |
|---|---|---|---|---|
| 1 | 3D Impact | one-shot | 空间核心、球形冲击、地面波、碎片 | 平面圆贴图冒充 3D |
| 2 | 3D Aura | sustained | 脚底环、绕身轨道、上升能量 | 只有一个 Billboard 环 |
| 3 | 3D Area | sustained | 地面范围、体积边界、周期 Tick | Gameplay 范围由 VFX 反推 |
| 4 | 3D Beam | event-driven | 两端点、空间主束、噪声副束 | 长度变化导致纹理拉伸 |
| 5 | 3D Trail | sustained | 运动头、世界空间历史轨迹、碎屑 | 静态长条冒充 Trail |
| 6 | 3D Shield | event-driven | 包围壳、Fresnel、命中波 | 平面六边形冒充球形护盾 |
| 7 | 3D Spawn | one-shot | 地面门、上升柱、实体出现时点 | 自行生成 Gameplay 权威对象 |
| 8 | Environment/Weather | sustained | 相机区域、近中远密度、雾/降落物 | 打包成普通单次技能 |
| 9 | Screen/UI | event-driven | 屏幕边缘、中心反馈、安全区/Mask | 依赖 3D Camera FOV |

## 3. 共同验收门槛

- 九格必须使用同一保存的 Preview Scene 和唯一审查相机；Screen/UI 的实际渲染仍必须由 Canvas 语义产生。
- 3D 格必须从至少一个斜视角显示真实深度，不得只看正投影视图。
- Sustained/Event-driven 格跨九宫格周期持续；One-shot 格按周期重播。
- 每个正式候选有独立 Runtime Entry、Stop/Reset、资源所有权和递归依赖报告。
- 同步峰值帧每格有可辨主体；最终验证同时包含持续态和一次性清空态。
- 共享 Mesh/Material/Mask/Particle 必须只计一次；禁止为九格各自生成大 PNG。
- 人工验收检查形状、深度、材质层级、动作、遮挡和重复；机器检查不能以 `IsAlive` 或像素存在代替视觉质量。

## 4. 经验回写规则

本轮每个被用户指出的问题必须执行：事故记录 → 技术根因 → 修复候选 → 同相机 A/B → 最小防回归检查 → 提炼到 `docs/rules/60_ENGINEERING_LESSONS.md`。只修当前 Prefab 而不递归规则，视为未完成。
