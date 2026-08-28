# Frost Family ArtSource

这里保存 Frost Family 的非运行时美术源文件。Unity Player 只能依赖导出的 Runtime Atlas，不能直接依赖本目录。

## 来源与处理

- `RawGenerated/`：2026-08-23 使用 Codex 内置 image generation 生成的原始 RGB 文件。目标是透明背景，但工具实际烘焙了浅色棋盘格，因此这些文件只保留用于溯源，不可直接交付。
- `Modules/`：由 `tools/vfx/build_frost_family_atlases.py` 清理 Alpha、Tight Crop 后得到的独立模块。
- `AtlasLayout/`：稳定单元 ID、Rect、Pivot、方向、源文件 SHA-256 和 Runtime Atlas SHA-256。
- `LegacyRuntime/`：从 Unity `Assets` 移出的上一版三纹理/三材质及其 `.meta`，只用于可恢复审计，不进入 Player。

## 生成提示摘要

- Broken Ring：完整、无遮挡、白青/深蓝裂纹、破碎不规则冰环；禁止中心爆发、大冰晶、规则霓虹圆。
- Shards：严格 3×2 独立单元，六个不同冰晶；原始单元根部在下、尖端朝 `+Y`。确定性导出时统一旋转为 Runtime `+X`，由 Unity Stretch Billboard 将 `+X` 对齐放射速度；禁止相互重叠。
- Mist Ring：完整、无遮挡、透明中心的冰雾环；禁止硬质粗圆线。

三项都以 `docs/vfx-reviews/frost_impact_2d/TARGET_EFFECT_v1.png` 作为风格和色彩参考，不是从完整概念图直接裁剪。

## 可复跑导出

```powershell
& 'C:\Users\admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tools/vfx/build_frost_family_atlases.py
```

运行时输出：

- `project/Assets/VFX/Shared/Frost/Textures/T_Frost_ImpactAtlas_A_v1.png`
- `project/Assets/VFX/Shared/Frost/Textures/T_Frost_ShardAtlas_A_v1.png`
