# 2D 特效九宫格 V2 生产报告

日期：2026-08-23  
状态：V1 视觉失败已记录；V2 工程门禁与主 Agent 技术审图通过；用户于 2026-08-24 签署为**有条件通过**，不等同于无条件视觉通过或商用级认可  
唯一预览场景：`Assets/VFX/Preview/VFXPREVIEW_ValidationGallery_3x3.unity`

## 1. 本轮结论

上一版 Aura、Beam、Trail、Shield、Spawn 只有三层 Quad 和规则数学遮罩，虽然可运行，但更像 UI 图标，视觉不合格。本轮按 V2 重建为“主体、内部高光、外部能量、次级粒子、消散”五种职责，并继续保留 Projectile、Impact、Slash、Area 作为正式对照项。

九宫格只是一套 Preview 验收工具。每格仍引用独立 Runtime Entry；标签、布局、同步重播和相机均不进入正式游戏 Prefab。

## 2. V2 的五类视觉结构

| 类型 | V2 视觉职责 |
|---|---|
| Aura | 能量场、断续弧、内部旋流、轨道节点、扩散脉冲 |
| Beam | 外辉、核心束、分叉、端点、充能粒点 |
| Trail | 有机笔触尾迹、热脊、运动头、侧向纹理、离散碎片 |
| Shield | 半透明填充、双层边界、内部晶格、能量流、六边形受击脉冲 |
| Spawn | 传送门主体、符号段、内部旋涡、生成柱、中心闪光 |

每个 V2 Prefab 为 `7` 个 GameObject、`6` 个 Renderer、`1` 个共享嵌套 ParticleSystem，并且根节点只有一个 `IVfxRuntimeEntry`。Idle 状态由 Prefab 序列化为不可见，Play/Stop/Pool Reset 统一管理 Renderer 和粒子。

## 3. 共享纹理与素材策略

本轮使用内置图像生成能力制作了一张候选 2×2 灰度 Mask Atlas，内容为能量笔触、烟雾、碎片和火花/符号。原始 1024×1024 候选为 `1,276,967 B`，因不符合极简共享资产目标而明确拒绝进入项目。

正式项目只保留降采样后的：

- `Assets/VFX/Shared/ValidationGallery/Textures/T_ValidationGallery_MaskAtlas_128.png`
- 尺寸：`128×128`
- PNG 源文件：`18,188 B`
- 用途：局部破形和粒子轮廓，不是完整效果图，也不是序列帧

五类效果共享同一 Atlas、Shader、Quad Mesh、两份 Material 和一个 ParticleSystem Prefab；没有为每个效果复制独占大图。

## 4. 正式资产与体积

每个新 Effect 的 `Assets/VFX/Generated/<effect-id>/` 仍只有一个 Runtime Prefab。粒子系统曾被重复序列化进五个 Prefab，候选合计约 `656,625 B`，已拒绝。现在改为共享嵌套 Prefab：

| 项目 | 源文件大小 |
|---|---:|
| 五个正式 Runtime Prefab | `87,316 B` |
| 共享 ParticleSystem Prefab | `117,198 B` |
| Shader + Mesh + 2 Material + 128 Atlas | `36,399 B` |
| 正式 Prefab + 全部共享渲染依赖 | `240,913 B` |

这些是项目 Source/YAML 数字，不冒充 Player Build 大小或 GPU 驻留。与“每个 Prefab 内复制完整 ParticleSystem”的候选相比，正式 Prefab加共享粒子约从 `656 KB` 降到 `204 KB`，同时保持画面不变。

## 5. WYSIWYG 证据

权威目录：`docs/vfx-reviews/validation-gallery-3x3/evidence/current-run/`

主帧 `gallery_018.png` 使用保存的唯一 Main Camera、自然 Update 和 Preview-only 同步调度：

- `aliveEntries=9`
- `foregroundPixels=97,504`
- 九格前景像素：`[6521, 7624, 2333, 18430, 6178, 9267, 9724, 12436, 13630]`
- `whiteClipRatio=0.017404`

`gallery_228.png` 是持续预览改造前的完成态证据，当时 `aliveEntries=0`。2026-08-23 后 Preview Driver 改为 Aura、Area、Beam、Trail、Shield 跨周期持续，Projectile、Impact、Slash、Spawn 按周期重播；因此该完成帧只保留为旧行为记录，不再代表当前预览生命周期。待 Unity 关闭后应重新采集持续态权威证据。Aura 与 Shield 仍采用几何魔法结构，这是设计方向；其他三类已经不再是单一线条或图标。

## 6. 验证结果

- Unity Compile：通过
- EditMode `ValidationGalleryProductionTests`：`3/3`
- PlayMode `ValidationGalleryRuntimeTests`：`1/1`
- Graphics PlayMode `ValidationGalleryVisualCaptureTests`：`1/1`
- 当前没有 Unity 进程或项目锁

工程验证证明资产结构、运行时生命周期、共享依赖、同步预览和完成清理成立。用户已完成本场景的视觉签署，结论为下文“有条件通过”；机器门禁不因此折算为无条件视觉通过，本报告也不声明商用就绪。

## 7. 用户查看流程

1. 打开 Unity 项目 `project/`，等待编译结束。
2. 点击 `Tools > VFX Composer > Validation Gallery > Build + Open 3x3 Gallery`。
3. 切换到 `Game`，选择 `16:9`，Scale 建议 `1x`。
4. 点击 Play；九格会自动同步循环。
5. 重点检查：五类 V2 是否一眼可区分、Trail 是否像运动尾迹、Beam 是否有分叉和端点、Spawn 是否有生成层级、Aura/Shield 的几何感是否符合目标。

## 8. 用户最终视觉结论

- 签署日期：2026-08-24。
- 签署人：用户（本任务签署）。
- 结论：**有条件通过**。
- 用户原话：**“这个我已经审核过了，还是看似通过，但是无法做商用级别”**。

用户未指定具体格子、失败时间阶段或解除限制的条件。本报告不推断“无法做商用级别”的原因，不将该结论改写为无条件通过，也不据此授权重做或修改源码/资产。
