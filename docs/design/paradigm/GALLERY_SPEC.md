# 九宫格画廊场景规格（2D / 3D Gallery Scenes）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1` §8 验收面 / §4bis-6 Bloom 可开关）、`docs/design/references/REFERENCE_ANALYSIS.md`（截帧并排对比的质感标尺）
配套文档：`PROTOTYPE_CATALOG_v1.md`、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、四份技术族规格、`COMPILER_BOUNDARY_V2.md`
复用的既有能力：`Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs`、`Runtime/Diagnostics/W24CaptureProfile.cs`、`Runtime/Diagnostics/W24EvidenceStore.cs`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **维度轴** | **2D / 3D 各一个独立场景**（正交/透视相机、Sorting Layer/光照环境各异），ADR-010 §8 的必交产物 |
| **原型轴 / 元素轴 / 风格轴** | 画廊是**展示面**不新增内容：九宫格布局 + 左右翻页可遍历任意"原型 × 元素 × 风格 × 档位"子集；页组织方式给方案与理由（§4） |
| **技术族轴** | 5 族的产物均可被画廊格加载；画廊自身**零技术族内容**（它是场景，不是资产，§7） |
| **档位轴** | 六档产物均可加载；提供档位对比页（同一 recipe 六档并排，§4.3） |
| **验收面** | ADR-010 §8 的"用户对 3 原型 × 3 元素 × 3 风格交叉出的 9 格做一次判定"由 §4.2 的默认页组织直接支持；截帧对比（§6）复用既有 `W24ContinuousCaptureRecorder` |

**纪律**：画廊场景可以有相机、后处理 Volume、环境光——因为**它是场景不是资产**（ADR-010 §4 的"资产外一律不做不碰"约束的是预制体内部）。但这些场景配置**绝不得混入被验收的预制体**，§7 给出机器可查的隔离谓词。

---

## 1. 两个场景

| | `VFXGallery_3D.unity` | `VFXGallery_2D.unity` |
|---|---|---|
| 路径 | `Assets/VFX/Gallery/VFXGallery_3D.unity` | `Assets/VFX/Gallery/VFXGallery_2D.unity` |
| 相机 | Perspective，FOV 45°，位置 `(0, 4.5, -11)`，朝向格阵中心，`allowHDR = true`，`allowMSAA = false` | Orthographic，`orthographicSize = 6`，位置 `(0, 0, -10)`，`allowHDR = true`，`allowMSAA = false` |
| 渲染器 | URP Universal Renderer（3D） | URP **2D Renderer**（Light2D 生效的前提） |
| 背景 | 纯色 `(0.035, 0.04, 0.055, 1)`（与 `W24CaptureProfile` 的默认 `Background` 一致，使截帧无需改配置） | 同 |
| 环境光 | Environment Lighting = Color，`(0.06, 0.065, 0.08)`；**无 Directional Light**（见 §1.1） | Global Light2D 一盏，`intensity = 0.15`，`blendStyle 0`（2D 下没有它连不发光的层都看不见） |
| Sorting Layer | 不适用 | `Background / Default / VFX_Under / VFX / VFX_Over / UI`（画廊工程预置，产物的 `sorting.layer` 引用它们） |
| 地面 | 一块 20×20 的哑光灰平面（`(0.10, 0.10, 0.11)` 粗糙度高），供贴地层与阴影可见 | 一条地平线带（Sprite，`Background` 层），供贴地层与"地面线"语义可见 |
| 网格参考 | 3×3 的暗色地面格线（区分九宫格边界） | 同（Sprite 线） |
| Post Volume | 一个 Global Volume，仅含 Bloom + Tonemapping（§5） | 同 |

### 1.1 3D 场景为什么不放 Directional Light

放一盏平行光会让"局部光是否在工作"变得不可判读——被平行光照亮的实体壳与被特效自带 Point Light 照亮的看起来差不多。画廊的职责是**验收特效自身**，因此只留极弱环境光（让完全不发光的中性预制体仍有轮廓），一切亮度来自特效自带的局部光与自发光材质。

代价：`element: none` 的中性预制体在 3D 下会很暗。对策见 §4.4 的"结构检视模式"。

### 1.2 场景内容的完整清单

两个场景各自只含以下对象，不多不少（谓词 GA-1 断言）：

```
GalleryRoot
├─ Main Camera                     （+ 3D: 无额外组件 / 2D: PixelPerfectCamera 关闭）
├─ Global Volume                   （Bloom + Tonemapping，§5）
├─ Ground                          （地面平面 / 地平线带）
├─ GridLines                       （3×3 参考格线）
├─ Cells/                          （九个格位锚点）
│  ├─ Cell_0_0 … Cell_2_2          （空 GameObject，产物实例挂在其下）
├─ GalleryController               （翻页 / 播放 / Bloom 开关 / 截帧驱动，§2~§6）
├─ GalleryUI                       （Canvas，Screen Space-Overlay：页码、格标签、按钮，§3.2）
└─ CaptureRig                      （W24ContinuousCaptureRecorder + 配置，§6；默认禁用）
```

---

## 2. 九宫格布局

### 2.1 格位

3×3，行优先编号 `Cell_<row>_<col>`（row 0 = 上）。

| | 3D | 2D |
|---|---|---|
| 格间距 | 3.5 世界单位（X）× 3.0（Y） | 4.0（X）× 3.2（Y） |
| 格位 Z | 全部 0；相机透视下上排略远（视觉上自然分层） | 全部 0；格间用 Sorting Layer 不冲突（各格产物的 `sortingOrder` 加 `格序 × 100` 的偏移，见 §2.3） |
| 格内产物的缩放 | 由格的 `cellScale`（默认 1.0）统一控制，使不同尺度的原型可比 | 同 |

### 2.2 格的组成

```
Cell_r_c
├─ Anchor            （产物实例挂点）
├─ TargetProxy       （一个中性胶囊/圆形，供"作用于目标"类原型的 rendererRef/transformRef 使用）
├─ GroundProxy       （小块贴地参考，供贴地层）
└─ Label             （TextMeshPro，World Space：显示 "archetype × element × style × tier"）
```

`TargetProxy` 的存在使消散 / 附着 / 侵蚀 / 束缚这类"作用于外部目标"的原型在画廊里也能完整展示。**它是画廊的一部分，不是预制体的一部分**（§7 谓词 GA-4 断言产物不引用它以外的东西——产物通过接口参数接收它，接口为空时按 `COMPILER_BOUNDARY_V2.md` §5.6 的退化规则工作，因此"接了 TargetProxy"与"没接"两种状态都可在画廊里对比）。

### 2.3 2D 的格间排序隔离

2D 下所有格在同一平面，若各格产物的 `sortingOrder` 相同会互相穿插。规则：**格 `(r,c)` 内的全部渲染器的 `sortingOrder` 加上 `(r*3+c) * 100`**，由 `GalleryController` 在实例化后统一施加（不改预制体资产，只改实例的运行时值）。100 的间隔容纳单个产物内部 −40 ~ +30 的层偏移（`TECH_FAMILY_SPEC_MATERIAL.md` §11）且有余量。

---

## 3. 左右翻页

### 3.1 方案裁定

| 方案 | 描述 | 裁定 |
|---|---|---|
| A：仅键盘 | ←/→ 翻页，Space 重播，数字键跳页 | 不足（用户在 Game 视图里点了才有焦点，且不可发现） |
| B：仅 UI 按钮 | 屏幕左右各一个大按钮 | 不足（截帧时按钮会入画） |
| C：仅自动轮播 | 定时翻页 | 不足（无法停在想看的页上细看） |
| **D：三者全上（采用）** | 键盘 + UI 按钮 + 可开关的自动轮播 | **是**。理由：三种输入服务三种场景——键盘服务开发者快速遍历；UI 按钮服务"点 Play 就能用"的零学习成本（ADR-010 §8："切到目标场景点 Play 即开始检查"）；自动轮播服务录屏与无人值守截帧。UI 在截帧时由 `GalleryController` 自动隐藏（§6.2） |

### 3.2 交互清单

| 输入 | 动作 |
|---|---|
| `←` / `→`，或 UI 的 ◀ / ▶ 按钮 | 上一页 / 下一页（循环） |
| `Space`，或 UI 的 ⟳ 按钮 | 重播当前页全部九格（先 `ResetForPool()` 再 `SendEvent("launch")`） |
| `1`~`9` | 单独重播第 n 格 |
| `0` | 全部停止（`SendEvent("end")`） |
| `B`，或 UI 的 Bloom 开关 | 切换 Bloom（§5） |
| `L`，或 UI 的循环开关 | 切换自动轮播（默认关；开启后每 `autoAdvanceSeconds`（默认 6 s）翻一页并自动重播） |
| `S`，或 UI 的结构模式开关 | 切换"结构检视模式"（§4.4） |
| `H` | 隐藏 / 显示 UI（截帧前手动隐藏，或由截帧流程自动） |
| 鼠标点击某格 | 单独重播该格；再次点击选中/取消选中（选中格加一个细边框，方便对照标签） |

### 3.3 页的数据源

```
Assets/VFX/Gallery/GalleryPages.asset   （ScriptableObject）
GalleryPageSet
├─ dimension: 2d | 3d
├─ pages: GalleryPage[]
│   ├─ title: string
│   ├─ mode: enum { Matrix3x3, ExplicitList, TierComparison }
│   ├─ rowAxis / colAxis: enum { Archetype, Element, Style, Tier, None }
│   ├─ rowValues[3] / colValues[3]: string
│   ├─ fixedArchetype / fixedElement / fixedStyle / fixedTier: string   // 未作为轴的维度取固定值
│   └─ explicitCells[9]: { prefabPath }                                  // 仅 ExplicitList 模式
└─ prefabRootPath: "Assets/VFX/Generated/"
```

`GalleryController` 按 `(archetype, element, style, tier)` 拼出 recipe id 与产物路径去加载；缺失的产物在该格显示一个"未编译"占位标记（**不报错、不中断**——画廊是检视工具，缺格是常态）。

---

## 4. 格子填充策略与页组织

### 4.1 默认填充：`element: none` 中性预制体（沿用 T2A_REPORT §5.3 建议）

第 1 页固定是"结构页"：九格 = 九个不同原型 × `element: none` × `style: none` × 同一档位。理由（T2a 已给，此处补一条实现层的）：

- 结构缺陷与元素/风格缺陷可以分开看（T2a 的理由）。
- **补充理由**：`element: none` 的中性预设保证任何原型都能编译出"结构可见"的产物（`ELEMENT_CATALOG_v1.md` §4 的默认注入规则），因此结构页对**任意 58 个原型**都成立，不依赖任何元素预设是否已实现。它是画廊里唯一一页在 T2c 阶段（元素只做了 3 个）就能铺满 58 个原型的页。

### 4.2 页组织方式：**按"固定两轴、变化两轴"的矩阵页（采用）**

| 方案 | 描述 | 裁定 |
|---|---|---|
| A：按原型翻页（每页一个原型 × 9 个元素） | 一页看一个原型的全部元素 | 不足。它把"原型是否成立"和"元素是否成立"绑在一起看，而这两件事的失败模式完全不同 |
| B：按元素翻页（每页一个元素 × 9 个原型） | 一页看一个元素在 9 个原型上的表现 | 有用但不够——同样缺"风格轴怎么看" |
| **C：矩阵页（采用）** | 每页显式声明"行轴 / 列轴"（从 `{原型, 元素, 风格, 档位}` 中取两个），另两轴取固定值 | **是** |

**理由**：三轴正交是 ADR-010 的核心主张，画廊的职责就是**让"正交"这件事可被肉眼验证**。矩阵页让每一页回答一个明确的问题："固定原型与档位，元素×风格的九种组合是否都成立？" —— 如果某一行整体失败，是元素的问题；某一列整体失败，是风格的问题；单格失败，是这个组合的问题。按单轴翻页做不到这种归因。

**默认页集（`GalleryPages.asset` 的初始内容，2D/3D 各一份）**：

| # | 页标题 | 行轴 | 列轴 | 固定 | 用途 |
|---|---|---|---|---|---|
| 1 | 结构页 | 原型（3） | 原型（3） | `element=none, style=none, tier=PM` | 九个不同原型的结构可见性（§4.1） |
| 2 | **范式判定页** | 原型（3） | 元素（3） | `style=cartoon, tier=PM` | **ADR-010 §8 的用户判定面**（3 原型 × 3 元素 × 1 风格 = 9 格） |
| 3 | 风格页 | 原型（3） | 风格（3：`none/cartoon/pixel`） | `element=<选定>, tier=PM` | 风格正交性：同一原型三种画法 |
| 4 | 元素页 | 元素（3） | 元素（3） | `archetype=<选定>, style=cartoon, tier=PM` | 九个元素在同一原型上的形态差异 |
| 5 | 档位页 | `TierComparison` 模式 | — | `archetype/element/style` 固定 | 同一 recipe 的六档并排（前 6 格）+ 3 格留空（§4.3） |
| 6 | 辉光页 | 辉光参数（3 组） | 辉光参数（3 组） | 同一 recipe | `falloffCurve × layerCount` 的九种组合，供"可调教"的直观验证（REFERENCE_ANALYSIS §3bis） |
| 7+ | 自定义 | — | — | — | 后续批次追加 |

### 4.3 档位对比页

`mode = TierComparison` 时，九格中前六格 = 同一 recipe 的 `ML/MM/MH/PL/PM/PH` 六个产物，第 7~9 格显示该 recipe 的成本报告摘要（World Space Text：八项成本 + 降级登记条目）。这一页专门验收两件事：

1. **降级不等于消失**：ML 档必须仍然"是那个特效"（局部光烘进材质、描边壳换暗边、GPU 粒子换 CPU 粒子）。
2. **接口一致**：六格同时响应同一次 `Space` 重播（`COMPILER_BOUNDARY_V2.md` CM-4 的肉眼版）。

### 4.4 结构检视模式（`S` 键）

3D 场景不放平行光（§1.1）导致中性预制体很暗。结构检视模式临时：

- 把环境光提到 `(0.35, 0.35, 0.38)`；
- 打开一个仅在此模式启用的"检视用" Directional Light（`intensity = 0.6`，从相机方向）；
- 关闭 Bloom；
- 在每个格的 Label 上追加层数与组件数。

**这盏光属于画廊场景，不属于任何预制体**——它只在这个模式下启用，且截帧流程强制关闭它（§6.2）。

---

## 5. Bloom 可开关（ADR-010 §4bis-6）

### 5.1 实现位置

**场景内一个 Global Volume + 一个开关脚本**，不是相机上的组件、不是预制体里的东西。

```
Global Volume（GalleryRoot 下）
├─ Volume（isGlobal = true, priority = 0）
└─ VolumeProfile: Assets/VFX/Gallery/GalleryVolume.asset
   ├─ Bloom          { threshold: 1.0, intensity: 0.6, scatter: 0.7, tint: white, highQualityFiltering: true }
   └─ Tonemapping    { mode: Neutral }
```

`GalleryController` 的 Bloom 开关直接置 `bloom.active = false/true`（`VolumeProfile.TryGet<Bloom>` 一次，缓存引用）。

**为什么保留 Tonemapping 且不做开关**：没有 tonemapping 时，HDR 亮部会硬裁到白，`sg_comp_hdr_grade` 的三到四个台阶在亮端会糊成一片（`TECH_FAMILY_SPEC_MATERIAL.md` §7.2 的分级读感失效）。Tonemapping 是"看得到分级"的前提，不是"看起来更漂亮"的修饰。用户工程里也几乎必开。若需要看纯线性输出，用截帧的 `effect-only` 通道（§6.3），那条路本就绕过后处理。

### 5.2 默认状态

**默认开启（`bloom.active = true`）**。理由：ADR-010 §4bis-6 的定义是"开 = 完整观感，关 = 资产裸质量"。用户 Play 后第一眼应该看到的是**完整观感**（这是产品交付给游戏开发者的实际效果），裸质量是诊断态。

### 5.3 切换方式与状态可见性

- `B` 键 / UI 按钮切换。
- 当前状态在 UI 上以文字常驻显示（`Bloom: ON` / `Bloom: OFF`），并写入截帧的 `CaptureProfile.Bloom` 字段——**截图必须能自证它是在哪种状态下拍的**（既有 `W24CaptureProfile` 已有 `Bloom` 与 `BloomValidation = "caller-frozen"` 字段，正好承载）。
- 切换不改变任何预制体状态，不触发重播。

### 5.4 判据的机器化

ADR-010 §4bis-6 的判据是"光晕跟着特效走 = 可自带；光晕改变整个画面 = 归用户"。画廊提供的验证手段：

**关闭 Bloom 后，每个格的特效仍应有可见的光晕溢出**（来自自带辉光层）。这可以用截帧做定量检查：对同一帧的 `bloom-off` 截图，测量特效包围盒外 1.5 倍半径环带内的非背景像素比例，应 > 3%。低于该值说明该产物的自带辉光层没起作用（或没配），是一条可自动化的验收信号（§6.5 的 `glowSpill` 指标）。

---

## 6. 截帧对比

### 6.0 复用什么

既有的 `W24ContinuousCaptureRecorder` + `W24CaptureProfile` + `W24EvidenceStore` 提供的能力，**逐项核实自代码**：

| 能力 | 出处 | 画廊如何用 |
|---|---|---|
| 用场景的序列化 authority Camera 渲染到 RT 并存 PNG | `RenderToPng` / `CaptureBeauty` | 画廊相机即 authority Camera |
| effect-only 通道（透明清屏 + 窄 culling mask + 前景像素计数） | `CaptureEffectOnly` / `CountForeground` | 用于 §6.5 的覆盖度与光晕溢出指标 |
| 冻结的 Capture Profile（Unity/URP/GPU/色彩空间/HDR/MSAA/背景/分辨率/fps/seed/保留帧表）与其 sha256 | `W24CaptureProfile` | 画廊截帧的可复现性凭据 |
| 源哈希（场景/预制体/manifest/工具）绑定 | `W24CaptureSourceHashes` | 把截图绑到具体产物版本 |
| 写一次的证据目录 + 封存 | `W24EvidenceStore` / `Complete()` | 输出目录（§6.4） |
| `Time.captureFramerate` 固定帧率 | `BeginInternal` | 保证第 N 帧总是同一时刻 |
| 保留帧表（只允许拍 `RetainedFrameIndices` 内的帧） | `CaptureFrameCore` | 画廊只拍固定的几个节拍点（§6.3） |

**接线方式**：新增一个 `GalleryCaptureDriver`（画廊场景组件，不是产物组件），它：

1. 持有 `W24ContinuousCaptureRecorder` 引用（`CaptureRig` 上），设置其 `authorityCamera` = 画廊相机、`diagnosticEffectLayers` = 特效实例所在层。
2. 构造 `W24CaptureProfile`：`Width/Height = 1920/1080`，`FramesPerSecond = 60`，`Background` 取画廊相机背景色（recorder 的 `BeginInternal` 会校验二者一致），`Bloom` = 当前开关状态，`RetainedFrameIndices` = §6.3 的节拍点表，`CanonicalSeed` = 当前页的 seed。
3. 构造 `W24CaptureSourceHashes`：`ScenePath` = 画廊场景，`PrefabSourcePath/Guid/Sha256` = 当前格产物，`ManifestSourcePath` = 该产物的 `BuildManifest.json`，`CaptureToolSourcePath` = `GalleryCaptureDriver` 自身源文件。
4. 调 `Begin(...)`（**非 formal 路径**：formal 的 `BeginFormal` 要求 batchmode + operator command hash，那是 W24 证据链的语义；画廊截帧是交互式检视工具，用 `Begin` 的 `allowNonBatchModeForTest` 语义更贴切——但既有 `Begin` 的默认参数为 false 会要求 batchmode，因此**画廊的批量截帧走 batchmode**，交互式截帧另走 §6.6 的轻量路径）。

### 6.1 两种截帧模式

| 模式 | 触发 | 路径 | 用途 |
|---|---|---|---|
| **批量截帧（权威）** | `tools/Invoke-Unity.ps1 -UseGraphics` 的 batchmode 入口，遍历 `GalleryPages.asset` 的全部页 | `W24ContinuousCaptureRecorder.Begin(...)` + `CaptureFrame(...)` | 交付物、与参考图并排对比、回归比对 |
| **交互式单格截图（便利）** | Play 中按 `P` | `ScreenCapture.CaptureScreenshot` 到 `test-results/gallery-shots/` | 开发者随手看，**不是证据**、不进对比流程 |

分开的理由：权威截帧必须有冻结的 profile 与源哈希（否则"这张图是哪个版本、什么配置下拍的"无法回答）；而开发过程中的随手截图不该被这套仪式拖慢。谓词 GA-7 断言交互式路径不写入证据目录。

### 6.2 截帧前的场景状态强制

`GalleryCaptureDriver` 在 `Begin` 之前**强制**：

1. 隐藏 `GalleryUI`（Canvas `enabled = false`）——UI 不得入画。
2. 关闭结构检视模式与其检视用 Directional Light（§4.4）。
3. 关闭自动轮播。
4. 关闭格线（`GridLines.SetActive(false)`）——参考图对比时格线是干扰。
5. Bloom 状态按当前 pass 设置（§6.3 的双 pass）。
6. 全部九格 `ResetForPool()` 后同一帧 `SendEvent("launch")`（保证九格节拍同步）。

复原在 `Complete()` 之后。谓词 GA-6 断言证据目录里的截图不含 UI（用一个已知的 UI 像素区域的方差检查近似断言，或更简单：断言 `GalleryUI.enabled == false` 被记入 capture metadata 的 semantic telemetry）。

### 6.3 拍哪些帧

`RetainedFrameIndices` 按**节拍点**而不是均匀采样：

| 帧号（60 fps） | 时刻 | 对应节拍 |
|---|---|---|
| 3 | 0.05 s | `launch` 起手 |
| 12 | 0.20 s | `launch` 峰值 |
| 30 | 0.50 s | `travel` / `sustain` 中段 |
| 60 | 1.00 s | `impact` 前后 |
| 75 | 1.25 s | `impact` 峰值后 |
| 120 | 2.00 s | `end` 衰减中 |
| 180 | 3.00 s | 残留 / 完全结束 |

7 帧 × 每帧 2 张（beauty + effect-only，recorder 自带）× 2 个 Bloom pass = **每页 28 张**。

**双 Bloom pass**：同一组帧拍两遍，`bloom-on` 与 `bloom-off`。这是 ADR-010 §4bis-6"开=完整观感，关=资产裸质量"的落地——两组图放在一起，用户一眼看出"自带辉光贡献了多少、Bloom 贡献了多少"。

### 6.4 输出目录与命名

```
test-results/gallery-capture/<yyyyMMdd-HHmmss>/<dimension>/<pageIndex>_<pageTitle>/
├─ capture-metadata.json          （recorder 写：profile / sourceHashes / frames）
├─ diagnostic-pass-manifest.json  （recorder 写）
├─ provenance.json                （recorder 的 Seal 写）
└─ frames/seed_<seed>/
   ├─ frame_00003_beauty.png
   ├─ frame_00003_effect-only.png
   └─ …
```

**为什么在 `test-results/` 而不是 `Assets/`**：ADR-007 §2.1 明确"构建日志、NUnit 结果的输入暂存一律放在项目目录之外，不属于项目写入面"。截帧产物是诊断输出不是资产，放进 `Assets/` 会污染依赖闭包并被 `VfxOutputAuditor` 的 stale 检查扫到。`test-results/` 是仓库已有的该类输出目录。

**命名规则**：`frame_<5 位帧号>_<pass>.png`（recorder 既有格式，不改）。目录层级承载 `时间戳 / 维度 / 页`；Bloom 状态记在 `capture-metadata.json` 的 profile 里（不进文件名——文件名已够长，且 profile 是权威）。批量截帧的两个 Bloom pass 各自是一次独立的 recorder 会话，因此是两个平行目录 `…/<pageIndex>_<title>__bloom-on/` 与 `__bloom-off/`。

### 6.5 与参考图的并排对比

**对比图的生成方式：脚本（采用），不是手工。**

| 方案 | 裁定 |
|---|---|
| 手工在图像软件里拼 | 否。每次截帧 28 张 × N 页，手工不可持续；且拼图的裁剪/缩放不一致会让对比失真 |
| **脚本生成（采用）** | 是。`tools/Build-GalleryComparison.ps1`（或同名 Python）读截帧目录 + `docs/design/references/*.png`，输出对比 sheet |

**脚本的行为**：

1. 输入：一个截帧目录 + 一份对比配置 `docs/design/paradigm/gallery-comparison.json`（**由 T2c 创建**，不在本卡范围；本文档只定格式）：
   ```jsonc
   { "pairs": [ { "referenceImage": "docs/design/references/6.png",
                  "compareAgainst": { "page": 2, "cell": "1_1", "frame": 60, "pass": "beauty", "bloom": "on" },
                  "note": "硬体+柔光的质感反差（REFERENCE_ANALYSIS §2-2）" } ] }
   ```
2. 输出：`test-results/gallery-capture/<ts>/comparison/<pairIndex>.png` —— 左参考图、右截帧，**等高缩放、黑底、下方标注**（参考图文件名 / 产物 recipe id / 帧号 / Bloom 状态 / note）。
3. 同时输出 `comparison/metrics.json`，含每对的四个可计算指标：

| 指标 | 计算 | 对应的质量准则 |
|---|---|---|
| `luminanceStops` | 对图像的非背景像素做亮度直方图，统计可辨识的峰的数量（峰间距 ≥ 2× 视为不同台阶） | REFERENCE_ANALYSIS §2-1「三到四个可辨识台阶」 |
| `edgeSharpnessRatio` | 高频能量（Laplacian 方差）与低频能量之比 | §2-2「硬柔并存」——比值过低 = 全糊，过高 = 全硬 |
| `anisotropyRatio` | 梯度方向直方图的主轴/次轴能量比 | §2-3「形态各向异性」 |
| `glowSpill` | 特效包围盒外 1.5 倍半径环带内的非背景像素比例 | §2-6「光晕溢出」；也是 §5.4 的自带辉光验证 |

**这四个指标不是验收判据**（美学由人判，ADR-009 §7），它们是**并排对比时的辅助读数**：当用户说"看起来不够"时，指标能指出是台阶数不足、还是全糊、还是没有光晕溢出。指标随对比图一起输出，供人判读。

### 6.6 交互式轻量截图

Play 中按 `P`：`ScreenCapture.CaptureScreenshot($"test-results/gallery-shots/{scene}_{page}_{cell}_{timestamp}.png")`。无 profile、无哈希、不封存、不进对比流程。谓词 GA-7 断言这条路径不写 `gallery-capture/` 目录。

---

## 7. 画廊自身的资产边界

### 7.1 边界声明

**画廊是场景，不是资产。** 它可以含：相机、后处理 Volume、环境光、Global Light2D、地面、格线、UI Canvas、控制脚本、截帧组件、`TargetProxy` / `GroundProxy`。

**但这些绝不得混入被验收的预制体。** 混入的三种典型方式与对应的机器检查：

| 混入方式 | 检查 |
|---|---|
| 产物里带了 Camera / Volume / Canvas / Directional Light | `COMPILER_BOUNDARY_V2.md` AU-1（组件闭集）+ §2.3 排除表 |
| 产物引用了画廊场景里的对象（`TargetProxy`、地面、相机） | `COMPILER_BOUNDARY_V2.md` RT-7（控制器不引用预制体外对象）+ 本文档 GA-4 |
| 产物依赖了画廊目录下的资产（`GalleryVolume.asset`、格线 sprite） | `COMPILER_BOUNDARY_V2.md` WL-5（依赖闭包必须在 `allowedDependencyRoots` 内；`Assets/VFX/Gallery/` **不在**该清单内） |

第三条是关键：**`Assets/VFX/Gallery/` 刻意不加入 `allowedDependencyRoots`**。这样任何"产物不小心引用了画廊资产"的情况都会被既有的 `E601` / `E8013` 依赖检查直接拒绝，不需要新机制。

### 7.2 画廊场景不进产物审计

反过来，画廊场景**不受** `VfxOutputAuditor` 的产物纪律约束（它不是产物）。但它有自己的一组谓词（§8），确保画廊本身不腐化。

### 7.3 画廊对产物的加载方式

`GalleryController` 用 `AssetDatabase.LoadAssetAtPath`（Editor）或 `Resources`/`Addressables`（Player）？**裁定：Editor-only 的 `AssetDatabase` 路径 + `#if UNITY_EDITOR`**。理由：画廊是开发期验收工具，不需要进 Player 构建；用 `Resources` 会把全部产物打进包，用 Addressables 会引入额外依赖。批量截帧走 batchmode Editor，同样可用 `AssetDatabase`。

因此 `GalleryController` 的加载逻辑在 `#if UNITY_EDITOR` 内，Player 构建中该场景不含有效格内容——这是可接受的（谓词 GA-8 断言画廊场景不在任何 Player 构建的 scene 列表中）。

---

## 8. 构造性谓词（EditMode 机器检查）

| 编号 | 谓词 |
|---|---|
| GA-1 | 两个画廊场景的根对象集合恰等于 §1.2 的清单（不多不少）。防止画廊被逐渐塞进临时对象后腐化成一个无法复现的场景 |
| GA-2 | 3D 场景中零个 `Light` 组件的 `type == Directional`**处于启用状态**（§4.4 的检视用光必须默认 `enabled = false`）；2D 场景恰好一个 `Light2D` 的 `lightType == Global` |
| GA-3 | 两个场景各有且仅有一个 `Volume`，其 profile 恰含 `Bloom` + `Tonemapping` 两个 override（不含 Vignette / ColorAdjustments / DepthOfField 等——那些会改变对参考图的判读基准） |
| GA-4 | `Cells/Cell_r_c` 恰好 9 个，命名与层次符合 §2.2；每格的 `Anchor` 下在编辑态为空（产物在运行时实例化，不烘进场景） |
| GA-5 | `GalleryPages.asset` 的每个页的轴声明自洽：`rowAxis`/`colAxis` 不相同、`rowValues`/`colValues` 长度为 3、未作为轴的维度有固定值 |
| GA-6 | 截帧流程的 capture metadata 中，semantic telemetry 记录了 `uiHidden: true` / `gridLinesHidden: true` / `inspectionLightOff: true` 三项（§6.2 的强制状态可自证） |
| GA-7 | 交互式截图路径写入 `test-results/gallery-shots/`，与证据目录 `test-results/gallery-capture/` 不重叠（§6.6） |
| GA-8 | 画廊场景不出现在 `EditorBuildSettings.scenes` 的任何启用条目中（§7.3） |
| GA-9 | `Assets/VFX/Gallery/` 不在 `AssetAllowList.json` 的 `allowedDependencyRoots` 内（§7.1 第三条的机器保证） |
| GA-10 | 画廊控制脚本（`GalleryController` / `GalleryCaptureDriver`）所在的程序集不被 `VFXComposer.Runtime` 引用（画廊依赖运行时，反向不成立） |

---

## 9. 审计清单

- [ ] 两个独立场景：相机（正交/透视）、渲染器（3D/2D Renderer）、Sorting Layer、光照环境、地面、格线各自给出。
- [ ] 3D 不放平行光的理由与"结构检视模式"的补偿。
- [ ] 九宫格布局：格位、格的四个组成（Anchor/TargetProxy/GroundProxy/Label）、2D 的格间排序隔离规则。
- [ ] 左右翻页：四方案对比 + 采用"键盘 + UI + 可开关轮播"三者全上 + 完整交互清单 + 页数据源资产形状。
- [ ] Bloom 可开关：实现位置（场景 Volume + 开关脚本）、默认开启及理由、切换方式、状态写入截帧 profile、Tonemapping 不做开关的理由、判据的机器化（`glowSpill`）。
- [ ] 格子填充：默认 `element: none` 中性预制体 + 补充理由；页组织三方案对比 + 采用矩阵页 + 归因论证 + 7 个默认页（含 ADR-010 §8 的范式判定页与档位对比页、辉光调教页）。
- [ ] 截帧对比：复用 `W24ContinuousCaptureRecorder` / `W24CaptureProfile` / `W24EvidenceStore` 的 7 项能力与具体接线、两种模式的分工、截帧前 6 项强制状态、7 个节拍帧 × 双 Bloom pass、输出目录与命名、对比图**脚本生成**方案与 4 个辅助指标。
- [ ] 画廊资产边界：可含什么 / 三种混入方式的机器检查 / `Assets/VFX/Gallery/` 刻意不入依赖白名单。
- [ ] 构造性谓词 10 条。
- [ ] 正文无具体特效名（页配置里的原型/元素/风格取值是数据不是正文）。
