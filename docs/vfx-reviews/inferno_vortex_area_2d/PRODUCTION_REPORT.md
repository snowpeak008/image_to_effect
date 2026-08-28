# Inferno Vortex Area 2D — Production Report

状态：`engineering pass / pending user visual acceptance`  
日期：`2026-08-23`  
Unity：`2022.3.62f3c1 / URP 14.0.12`

## 1. 交付结果

- Runtime Entry：`Assets/VFX/Generated/inferno_vortex_area_2d/VFX_inferno_vortex_area_2d.prefab`
- Preview Scene：`Assets/VFX/Preview/VFXPREVIEW_Area2D.unity`
- Recipe：`Assets/VFX/Recipes/Area/inferno_vortex_area_2d.default.json`
- Compiler：`area2d-10`
- Prefab GUID：`98a7cd2c6ddd20046b8e9cc6acebd175`
- Build Hash：`cd8cb3776e51f4bb188e322f12a5187a445f80aa5a47285418c80214693cb0e7`

正式 Generated 目录只有一个 Prefab 与 Unity `.meta`。参考图、证据、Recipe、Manifest、Shader、共享材质和共享纹理均不复制进 Generated。

## 2. 实现方式

主体不是序列帧，也没有使用完整效果截图。一个闭合 Ring Mesh 用于 Tick Pulse；其余火体使用三个复用 Quad Mesh，由 URP Shader 在极坐标中计算六臂旋涡、热纹、连续噪声和不规则外缘。两个低密度 ParticleSystem 只生成火焰微粒和方向性火星。Root 上唯一 Runtime Controller 提供 Play、Refresh、Tick、AllowTail Stop、Immediate Stop 与对象池 Reset。

## 3. 资源与运行预算

| 项目 | 实测 |
|---|---:|
| Runtime PNG 数量 | 1 |
| PNG 尺寸 | 256×256 |
| Source PNG | 3,421 B |
| 完整依赖纹理驻留 | 33,448 B |
| Local Texture | 0 B |
| Runtime Prefab YAML | 249,373 B（编辑器文本，不等于 Player 体积） |
| GameObject / 最大深度 | 8 / 1 |
| Renderer / Material | 7 / 2 |
| ParticleSystem / maxParticles | 2 / 48 |

PNG SHA-256：`AF8EF6252084270225F3C3CBFBE7A96EEAFE592446701F06FF828D88A868FF89`。正式 Manifest 记录完整 GUID、Dependency Hash、Source/Resident 成本和唯一 owned output。

## 4. 视觉证据

唯一当前证据目录：`docs/vfx-reviews/inferno_vortex_area_2d/evidence/current-run/`。

- `frame_009_ignition.png`：点火建立。
- `frame_042_established.png`：持续暗红火体与六臂亮纹。
- `frame_086_tick_pulse.png`：伤害 Tick 瞬间。
- `frame_130_stopping.png`：AllowTail 降温熄灭。
- `frame_147_complete.png`：完全清空。

证据来自保存的 Preview Scene、唯一序列化 Camera、PlayMode 自然 Update、单次 Play 与 Stop；没有 Emit、SetParticles、跳时采样或替代相机。目标参考只用于复杂度和色彩方向，不是 Runtime 依赖。

## 5. 失败与修复记录

1. 首轮把小 Mask 拉伸到弯曲 Mesh，产生规则线圈；人工拒绝。
2. 第二轮用两组正弦模拟裂隙，产生黑色渔网；人工拒绝。
3. 第三轮常亮 Core Ring 压住旋涡，读成普通火圈；人工拒绝。
4. 最终改为连续极坐标火体、自然噪声、六臂低频旋涡、小型热核；Pulse Ring 仅在 Tick 显示；废弃的两个实验 Mesh 及生成入口已移除。
5. 用户发现未播放时暴露原始 Quad。Compiler 现将全部 Renderer 序列化为关闭，`Play` 才统一开启，Stop/Pool 再全部关闭；粒子改为程序化圆形/菱形遮罩，消除了小方块和条纹；Tick Pulse 改为更细、更暗、更短。

这些结论已递归写入 `docs/rules/60_ENGINEERING_LESSONS.md` 的 `EXP-010/EXP-011`。

## 6. 验证结果

- Compile：exit 0。
- Area EditMode：5/5 passed。
- Area Runtime PlayMode：1/1 passed。
- Area Visual Capture PlayMode：1/1 passed。
- 完成帧前景像素：0；关键帧 white clip ratio：0。

以上只代表工程与证据链通过。最终视觉状态保持 `pending user visual acceptance`，由用户在 Unity Game View 中签署。
