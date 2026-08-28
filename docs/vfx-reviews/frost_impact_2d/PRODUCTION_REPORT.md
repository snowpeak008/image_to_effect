# Frost Impact 2D — Revision 3 Decomposed Production Report

状态：**`impact2d-29` 冰环无缝闭合候选已生成，待用户在 Unity 中视觉签署；不得提前声明视觉完成。**  
规则：`docs/rules/` `1.0-draft` + `25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md` 的“最大合理拆解”规则。

## 1. 正式产物

- Recipe：`project/Assets/VFX/Recipes/Impact/frost_impact_2d.default.json`
- Runtime Entry：`project/Assets/VFX/Generated/frost_impact_2d/VFX_frost_impact_2d.prefab`
- Preview：`project/Assets/VFX/Preview/VFXPREVIEW_Impact2D.unity`
- Manifest：`project/ProjectSettings/VFXComposer/BuildManifests/frost_impact_2d.manifest.json`
- 当前证据：`docs/vfx-reviews/frost_impact_2d/evidence/current-run/`

Runtime Prefab GUID 保持 `3120e8b907d830b44bd173bc26969daa`。当前 Compiler 为 `impact2d-29`，Recipe Revision 为 `3`，Build Hash 为 `fe297576d2d2ffba1e757b863f8febf60157cb378dd674c87c1dc74443914b7a`。

## 2. 本轮拆解结果

```text
目标图（Reference；不进 Player）
  ├─ 冰晶：256×256 / 2×2 静态变体 Atlas，12 个粒子复用
  ├─ 冰环：16 段确定性几何，合并为 1 Mesh + 1 Renderer
  ├─ 核心：程序化 Shader
  ├─ 雾：程序化 Shader + 8 个粒子
  └─ 雪点：程序化 Shader + 少量粒子
```

不再为完整冰环、Mist、Core 或 Mote 保存整张运行时图片。16 段冰环不是 16 个 GameObject、16 个材质或 16 张图，而是一个 224 顶点、192 三角形的共享 Mesh；逻辑扇区共享边界，视觉上连续无缝，冰体、冰脊和晶纹由同一程序化 Shader 生成。

旧 `T_Frost_ImpactAtlas_A_v1.png` 仍留在 `Assets` 作为视觉回滚材料，但当前 Manifest、Prefab 和 Player 依赖图均不引用它。用户视觉签署前不删除；签署后应移入 `ArtSource/VFX/Frost/LegacyRuntime/`。

## 3. 运行时组合

| 层 | 实现 | 运行职责 |
|---|---|---|
| FrostMistSegments | 1 ParticleSystem / 8 particles | 程序化低亮度空间雾 |
| BrokenIceRingSegments | 1 ParticleSystem / 1 combined Mesh | 16 段共享几何形成扩散冰脊 |
| IceShards_Large | 1 ParticleSystem / 6 shards | 复用 4 个静态冰晶变体 |
| IceShards_Small | 1 ParticleSystem / 6 shards | 同 Atlas、错开角度/速度/尺寸 |
| SnowMotes | 1 ParticleSystem / 7 particles | 程序化细雪/星点 |
| CoreFlash | 1 ParticleSystem / 2 particles | 两层程序化白青命中核心 |

合计：`7` 个 GameObject、最大深度 `1`、`6` 个 ParticleSystem、序列化容量 `33`、`6` 个 Renderer、`5` 个共享 Material。Runtime Prefab 内没有预览 Driver。

## 4. 大小与内存对比

| 指标 | Revision 2 全环 Atlas | Revision 3 拆解版 | 变化 |
|---|---:|---:|---:|
| 正式运行时 PNG 源文件 | `184,844 B` | `22,527 B` | `-87.8%` |
| 完整依赖驻留纹理 | `329,040 B` | `66,216 B` | `-79.9%` |
| 当前声明依赖源文件合计 | — | `62,046 B` | 含 Shader、Material、Mesh、Runtime 脚本 |
| 冰环 Mesh YAML | — | `23,431 B` | GZip 近似 `4,620 B` |
| Runtime Prefab YAML | `714,522 B` | `714,646 B` | 编辑器文本，不等于 Player 占用 |
| Runtime Prefab GZip 近似 | 约 `26.6 KB` | `29,803 B` | 仅用于解释文本压缩性，不是正式 Player Build 数据 |

关键结论：旧 162,317 B 的完整冰环/Mist/Core Atlas 已从正式依赖图移除；现在唯一的运行时 PNG 是 22,527 B 的冰晶静态变体 Atlas。Prefab 的 714 KB 是 Unity YAML 序列化源文件，不能直接当作打包后的运行时体积。

## 5. 验证结果

- Compile：通过。
- Impact EditMode：`5/5`，验证单一 Runtime Entry、16 段合并 Mesh、无 Legacy Impact Atlas 依赖、纹理驻留小于 100,000 B、Blend/生命周期和 Preview Scene。
- Impact Runtime PlayMode：`1/1`，正常播放并完成回池。
- Impact WYSIWYG Capture PlayMode：`1/1`，使用真实图形设备、同一序列化 Preview Camera、自然 Update、单次播放；无手工 Emit、SetParticles、跳时采样或替换相机。
- 完成帧：`0.483333s` 前景像素为 `0`。
- 当前无此项目的 Unity 进程。

本轮只运行 Impact 定向门禁，没有运行全项目测试套件或 Player Build，不得据此宣称全项目回归完成。

## 6. 当前视觉候选

- `0.05s`：双层白青核心与第一批冰晶。
- `0.116667s`：12 枚冰晶形成放射主体，16 段冰脊展开。
- `0.25s`：冰晶穿过外圈，中心退场，只剩细环、雾和雪点。
- `0.483333s`：完全清空。

机器测得核心帧白色裁切占前景 `2.5248%`，组合峰值 `0.2687%`；峰值 P95 亮度 `152.875`，衰减帧降至 `75.838`。这些只证明曝光、衰减和清空满足门禁，不证明艺术风格等于参考图。

`impact2d-21` 的冰环因基础 Alpha 过低而退化为单线，已明确判定视觉失败。`impact2d-22` 至 `26` 依次修复重复齿轮纹、放大的段间黑缝、边界位置不连续与冰体过暗。用户随后发现3点钟方向仍有首尾切口；`impact2d-29` 使末扇区顶点与首扇区顶点精确相等，并改用圆周周期噪声，当前真实帧中黑色水平接缝已消失。

参考图具有大量手绘冰纹、霜丝、雪花和多层光晕；当前候选优先验证“可拆、可复用、低纹理占用”的视觉 MVP。若用户认可构图但要求更高细节，下一步应增加一个受预算约束的小型 Frost Detail Atlas 或更复杂的程序化晶纹，而不是恢复整张完整效果图。
