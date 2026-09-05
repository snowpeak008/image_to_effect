# 首批两风格的材质族设计：卡通 / 像素（Style Implementation: Cartoon & Pixel）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）、`docs/design/references/REFERENCE_ANALYSIS.md`（质感标尺）
配套文档：`STYLE_CATALOG_v1.md`（风格定义与三处叠加机制）、`TECH_FAMILY_SPEC_MATERIAL.md`（`StyleStage` 插槽契约 §4）、`TECH_FAMILY_SPEC_PARTICLES.md`、`TECH_FAMILY_SPEC_MESH_LIGHT.md`、`COMPILER_BOUNDARY_V2.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **风格轴** | **首批 2 个全部落地**：卡通 `cartoon` 与像素 `pixel` 的 `StyleStage` 子图内部实现（逐节点算法）、对 5 技术族的约束落点、StylePreset 默认值表（含辉光层 18 参数的风格默认值）、风格 × 六档交互表、构造性谓词 |
| **技术族轴** | 风格的三处叠加点（`STYLE_CATALOG_v1.md` §1.2）在 5 族的具体实现位置全部给出：材质族（`sg_style_cartoon` / `sg_style_pixel`）、GPU/CPU 粒子族（形状/尺寸/颜色/旋转/位置约束）、网格族（细分/位移量化/描边壳）、局部光族（强度色阶/闪烁量化/阴影许可） |
| **原型轴 / 元素轴** | 不新增、不修改；风格零结构改动（不增删层、不改节拍、不改接口），只在编译期叠加。本文档给出"风格不覆盖元素的什么"的边界表（§4） |
| **维度轴** | 两风格各给 2D / 3D 差异；像素风的 3D 定位为体素感（ADR-010 §4bis-4） |
| **档位轴** | 六档全部有交互规格（§6）：卡通描边壳在 ML/MM 降级为材质暗边（ADR-010 §4bis-5）；像素抖动分档；色阶/时间量化与档位无关的理由 |

**纪律**：正文只用层角色 / 技术族 / 参数表述，无具体特效名。像素风走 shader 内量化、保持原分辨率、**不用低分辨率 RenderTexture、不用相机/后处理 pass、不烘任何序列帧**（ADR-010 §2 用户裁定 + §5 素材纪律）；时间量化 ≠ 序列帧的论证见 §3.5。

---

## 1. 风格叠加的三处落点（实现视角）

`STYLE_CATALOG_v1.md` §1.2 定义了三个叠加点。本文档给出每个叠加点在编译流水线中的**确切时刻**与**确切写入面**：

| 叠加点 | 编译时刻 | 写入面 | 是否进参数合并链 |
|---|---|---|---|
| ① 材质族 `StyleStage` 子图 | 材质实例化时 | 材质的静态关键字 `_STYLE_*` + `StyleStage` 槽位子图引用 | **否**——由 `style.id` 直接决定挂哪个子图（`STYLE_CATALOG_v1.md` §1.2 明确）；子图的可调参数来自 `style.parameters` |
| ② 粒子族 / 网格族约束参数集 | 参数合并的第 4 步 | 层参数值 | **是**——优先级高于元素预设、低于 recipe 显式参数 |
| ③ 局部光约束 | 参数合并的第 4 步 | `VfxLightBeat` 的字段 | **是**——同上 |

合并顺序（与 `ELEMENT_CATALOG_v1.md` §2.1 / `RECIPE_V2_SCHEMA_DRAFT.md` §3 一致）：

```
原型层默认 ← 元素预设(role, family) ← 元素预设(role.suffix, family) ← 【风格约束②③】 ← recipe 显式 parameters ← tierOverrides ← 档位预算截断
                                                                              ↑
                                                          【风格子图①】不在此链，由 style.id 直接决定
```

---

## 2. 卡通 `cartoon`

### 2.1 渲染取向与实现总纲

`STYLE_CATALOG_v1.md` §2.1：色阶分层 + 描边 + 锐利阈值 + 高饱和。

`sg_style_cartoon` 子图的内部流水线（严格按序，全部在 `TECH_FAMILY_SPEC_MATERIAL.md` §4.2 的 9 端口契约内）：

```
inColor, inAlpha, inEdgeDist, inNormalWS, inViewWS, inStepId, inPixelUv, inLayerKind
  ↓ A. 色阶分层（§2.2）
  ↓ B. 锐利阈值（§2.3）
  ↓ C. 描边 / 暗边（§2.4）
  ↓ D. 高饱和与色板重映射（§2.5）
  ↓ E. HDR 压台阶（§2.6）
  ↓ F. 内描线（可选，§2.7）
outColor, outAlpha, outOutlineColor
```

### 2.2 A. 色阶分层（cel shading）与色带避免

**基本量化**：

```
L      = luminance(inColor)              // 线性亮度
Lq     = floor(L * shadingSteps) / max(shadingSteps - 1, 1)
```

**色带（banding）问题**：直接量化亮度会在大面积平缓渐变处产生难看的横条纹（尤其在 `body / veil / surface` 这类低对比大面积层）。三条对策，**全部启用**：

| # | 对策 | 实现 | 代价 |
|---|---|---|---|
| 1 | **量化的是"形态场"而不是"最终亮度"** | 优先用 `inStepId`（`sg_comp_hdr_grade` 已经算好的强度台阶，`TECH_FAMILY_SPEC_MATERIAL.md` §7.2）作为分档依据，只有当 `stepCount != shadingSteps` 时才对亮度做二次量化。台阶来自噪声/SDF 场，天然有形态边界，不产生条带 | 0 |
| 2 | **梯度自适应的过渡宽度** | 用 `fwidth(L)`（屏幕空间导数）决定每个台阶边界的软化宽度：`t = smoothstep(edge - fwidth(L)*aaScale, edge + fwidth(L)*aaScale, L)`。渐变平缓处 `fwidth` 小 → 边界锐利；渐变陡峭处 `fwidth` 大 → 边界抗锯齿。**这同时解决了色带与阶梯锯齿两个问题** | 1 条 `fwidth` |
| 3 | **量化前加极低幅蓝噪声抖动** | `L += (blueNoise(inPixelUv) - 0.5) * bandDither`，`bandDither` 默认 `1/(shadingSteps*24)`（极小）。把硬边界打碎成像素级噪声，人眼感知为平滑。仅当 `inLayerKind == volume` 时启用（体积层是色带重灾区，实体层不需要） | 1 次哈希 |

**分档取色**：

```
color = paletteDiscrete(Lq)       // 按 cool → primary → secondary → hot 的 5 色离散取色
                                   // 不插值（这是"每档取色板固定一色"的含义）
```

**`shadingSteps` 的选择**：2 = 极简（两色），3 = 默认（暗 / 主 / 亮），4 = 细腻。上限 4——超过 4 档在视觉上已回归渐变，失去卡通读感（这是 schema 里 `shadingSteps ∈ [2,4]` 的理由）。

**3D 的真 cel 明暗**：`inLayerKind == solid` 且主图是 `SG_VfxLit` 时，量化对象是 `NdotL`（法线-光方向点积）而不是 `luminance(inColor)`，得到真正的赛璐璐明暗分层。其余情况（透明体积、quad、2D）一律按亮度/场量化（`STYLE_CATALOG_v1.md` §2.6）。

### 2.3 B. 锐利阈值

```
// 元素给的阈值宽度 w0，卡通把它压窄：
w = lerp(w0, 0.005, edgeSharpness)
```

`edgeSharpness ∈ [0,1]`，默认 0.85。**实现位置的关键点**：这个压窄**不在 `sg_style_cartoon` 内部生效**——`StyleStage` 位于阈值之后，改不了已经算完的 alpha。因此 `edgeSharpness` 是一个**编译期参数注入**：编译器把它写进该层 `sg_threshold_soft` 的 `width` 属性。

这是风格叠加点①的一个例外情形，需要明确记录：**`edgeSharpness` 与 `detailMul` 两个卡通参数走的是"编译期改写上游子图属性"，不是 `StyleStage` 内部计算**。谓词 CT-4 断言这两个参数被正确写入了上游属性。

### 2.4 C. 描边与低端档暗边替代（ADR-010 §4bis-5）

| 情形 | 实现 | 档位 |
|---|---|---|
| **3D 实体层**（`inLayerKind == solid`） | **法线外扩描边壳**：层节点下的 `Outline` 子节点，共享同一 Mesh，材质为 `SG_VfxLit` 的 `Cull Front` + `sg_vdisp_shell_extrude`（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.22）。壳的颜色 = `outOutlineColor` | MH+ |
| **3D 实体层，低端档** | **材质暗边**：`sg_edge_dark_rim`（`TECH_FAMILY_SPEC_MATERIAL.md` §3.4）。用菲涅尔量 `f = pow(1 - saturate(dot(N,V)), rimPower)` 圈出边缘区域，把该区域的颜色**乘暗**：`color *= lerp(1, darkness, smoothstep(1 - rimWidth, 1, f))` | **ML / MM**（描边壳禁用） |
| **2D 层 / 3D 的 quad 类层** | **SDF 描边**：`outline = 1 - smoothstep(0, outlineWidth, inEdgeDist)`，`color = lerp(color, outlineColor, outline)`。2D 下同时把描边层的 `sortingOrder` 设为宿主 −1（`STYLE_CATALOG_v1.md` §2.6） | 全档 |
| **粒子材质** | 粒子 quad 内的 SDF 外圈描边（同上，用粒子形状的 `edgeDist`） | 全档 |
| **透明体积层**（`inLayerKind == volume`） | **不描边**：`outlineWidth` 对这类层的默认值为 0（`STYLE_CATALOG_v1.md` §2.7：加描边会破坏体积感） | — |

**暗边与真描边的观感差异（诚实记录）**：真描边壳是**等宽的、在轮廓外侧的**一圈线，无论表面朝向如何都一致；材质暗边是**在轮廓内侧的、宽度随视角变化的**一圈渐暗。二者在静态截图上接近，在旋转时暗边会"呼吸"。这是 ML/MM 档的已知取舍，画廊将并排展示（`GALLERY_SPEC.md` §4.3 的档位对比页）。

`outlineColor` 默认 = 元素 `cool` 色 × 0.4（不是纯黑——纯黑描边在高饱和色板下会显脏）。

### 2.5 D. 高饱和与色板重映射

```
// 在 HSV 近似空间做（避免完整 RGB↔HSV 转换的成本）：
lum   = luminance(color)
color = lum + (color - lum) * saturationMul        // 绕亮度轴拉开饱和度
color = pow(color / max(lum, 1e-4), 1.0) * applyValueCurve(lum)   // 明度曲线提中间调
```

`valueCurve` 用一条三点曲线的解析近似：`v' = v * (1 + midBoost * 4 * v * (1 - v))`，`midBoost` 默认 0.25——它在 v=0.5 处提升最多，两端不变，正好是"提亮中间调"的含义且不破坏黑与白。

`saturationMul` 默认 1.3。**上限 1.5**：更高会把 HDR 亮部推出色域，在 Bloom 下产生色偏。

### 2.6 E. HDR 压台阶

```
if (lum > hdrClamp):
    color = lerp(color, paletteHot, highlightBlend)      // 统一输出 hot 色
    color = max(color, hdrClamp)                          // 保底不低于 clamp（保证仍会 bloom）
```

**卡通的 HDR 处理不是"截断到 1.0"**：那样用户开 Bloom 时不会有溢出，与 REFERENCE_ANALYSIS §2-6 冲突。做法是把 `> hdrClamp` 的所有亮度**压成同一个高光块**（不做连续过曝渐变），但这个块的亮度仍 > 1。`hdrClamp` 默认 1.5，`highlightBlend` 默认 0.8。

这保持了 `TECH_FAMILY_SPEC_MATERIAL.md` HG-2 谓词（`hot` 台阶亮度 ≥ 1.5）在卡通风格下依然成立。

### 2.7 F. 内描线（可选）

在色阶跳变处加一条细暗线，增强"勾线"感：

```
band = Lq                              // 量化后的档位值
d    = fwidth(band)                    // 档位在屏幕空间的变化率：跳变处非零，档位内部为 0
line = smoothstep(0, innerLineWidth * d_ref, d)   // d_ref 为归一化常数
color = lerp(color, outlineColor, line * innerLineStrength)
```

`innerLine` 默认 false（成本 1 条 `fwidth`，且对体积层易产生噪点）。开启时只作用于 `inLayerKind == solid`。

### 2.8 与 REFERENCE_ANALYSIS §2「硬柔并存」如何兼容

这是卡通风格最容易出问题的地方：**卡通把一切都变硬，会丢掉参考图里"硬体 + 柔光"的反差**（§2-2：单一柔度的产物一定显得廉价——但单一硬度同样显得廉价，只是失败方式不同：显得平、像贴纸）。

四条兼容措施：

| # | 措施 | 落点 |
|---|---|---|
| 1 | **辉光层不被量化** | `Glow_k` 子节点的材质用 `SG_VfxAdditive`，其 `StyleStage` 在卡通下用 `falloffCurve = step` 得到**阶梯光圈**（硬边的柔光），但 `layerCount ≥ 2` 保证多层叠加仍有厚度。即：卡通的"柔"由多层阶梯光圈的叠加提供，不是高斯模糊 |
| 2 | **`veil` 层豁免色阶量化** | `inLayerKind == volume` 且该层是 `sg_comp_veil_layer` 产物时，`shadingSteps` 被强制为 `max(shadingSteps, 4)` 且 `bandDither` 开——低亮度大范围雾幕（§2-4）如果被压成 3 档会变成三个色块，彻底破坏体量感 |
| 3 | **强度台阶不被压平** | 卡通的色阶量化作用于**颜色映射**，`sg_comp_hdr_grade` 的强度台阶（§2-1 的三到四个可辨识台阶）在上游已经算好且倍率关系不变。因此 HG-1 谓词（相邻台阶亮度比 ≥ 2.0）在卡通下同样通过 |
| 4 | **各向异性不被风格改变** | `aniso`（§2-3 形态各向异性）是元素参数，卡通不覆写。风格改的是"边缘怎么画"，不是"形态是什么形状" |

**验收方式**：画廊的卡通页必须同时可见"锐利的主体轮廓"与"多层柔光晕"（`GALLERY_SPEC.md` §6.3 的对比判据）。

### 2.9 卡通 × 5 技术族约束落点

| 族 | 约束 | 实现位置 |
|---|---|---|
| 材质 | 色阶 / 锐利阈值 / 描边 / 高饱和 / HDR 压台阶 | `sg_style_cartoon`（§2.2~2.7）+ 编译期改写 `sg_threshold_*.width`、噪声细节层权重 `detailMul` |
| GPU 粒子 | 基础形替换（圆→带描边的圆/多角星 SDF；拉伸 billboard 长宽比 ≤ 3:1）；`StartSize` 量化到 3 档；`ColorOverLife` 转阶跃梯度；密度倍率 0.6 | Output 的材质变体 + Initialize 块的量化节点 + 编译期梯度离散化（`TECH_FAMILY_SPEC_PARTICLES.md` §6.3） |
| CPU 粒子 | 同上；`ColorOverLifetime.mode = Fixed`；`Trails.widthOverTrail` 3 段阶梯 + 末端硬截断 | 模块字段编译期设置 |
| 网格 | `maxSubdivision` 中档（低多边形更卡通）；顶点位移幅度量化 3 档；`outlineShell = true`（MH+）；碎块保留硬边、断面纯色 | 生成器参数上限 + `sg_vdisp_*` 的量化开关 + `Outline` 子节点（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.22） |
| 局部光 | `intensitySteps = 3`；`flickerQuantize = true`；`saturationMul = 1.2`；允许阴影（建议硬阴影） | `VfxLightBeat` 字段（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §6.2） |

### 2.10 卡通 2D / 3D 差异

| 项 | 3D | 2D |
|---|---|---|
| 描边 | 实体层法线外扩壳（MH+）/ 材质暗边（ML/MM）；quad 与粒子用 SDF 描边 | 全部 SDF 描边；描边层 `sortingOrder` = 宿主 − 1 |
| 色阶依据 | 实体层量化 `NdotL`（真 cel）；透明体积层量化亮度 | 全部量化亮度 / 噪声场（无法线光照） |
| 排序 | 描边壳 `Cull Front` + Queue 偏移；透明队列内按 `TECH_FAMILY_SPEC_MATERIAL.md` §6.4 表 | 显式 Sorting Order（同上文档 §11 表） |
| 局部光 | `Light`（Point/Spot）阶跃强度 | `Light2D` 阶跃强度 |
| 辉光 | `Glow_k` 在宿主之后画 | `Glow_k` 在宿主之下画（`TECH_FAMILY_SPEC_MATERIAL.md` §11 的 2D 辉光排序裁定） |

---

## 3. 像素 `pixel`

### 3.1 渲染取向与实现总纲

`STYLE_CATALOG_v1.md` §3.1：shader 内量化到虚拟像素栅格 + 有限色带 + 硬边 + 粒子/几何吸附栅格；**保持原渲染分辨率**。

像素风的实现分成**两半**，这是它与卡通最大的结构差异：

| 半 | 位置 | 内容 |
|---|---|---|
| **坐标量化**（上游） | `sg_flow_grid_snap`（`TECH_FAMILY_SPEC_MATERIAL.md` §3.5），在**全部形态计算之前** | 把采样坐标吸附到栅格，使噪声/SDF/阈值全部在量化坐标上求值——**形态边缘因此自然呈阶梯像素**，而不是先算平滑形状再后处理 |
| **颜色量化**（下游） | `sg_style_pixel`（`StyleStage`） | 颜色分级、alpha 二值化、抖动、1 格描边 |

**为什么坐标量化必须在上游**：如果只在 `StyleStage` 做颜色量化，形态边缘仍然是平滑的（只是颜色分了级），得到的是"色阶画"而不是"像素画"。像素感的本质是**空间量化**。

### 3.2 栅格空间的选择（UV / 世界 / 物体，ADR-010 §4bis-4）

`snapSpace` 四值，按维度与层类别选择：

| `snapSpace` | 计算 | 默认适用 | 优点 | 缺点 |
|---|---|---|---|---|
| `uv` | `floor(uv / s) * s` | quad / billboard / UI 层 | 简单，随物体缩放 | 不同层的栅格不对齐（各自 UV 尺度不同） |
| `worldXY` | `floor(worldPos.xy / s) * s` | **2D 默认** | 正交相机下**所有层栅格天然对齐**，多层特效像同一张像素画 | 3D 透视下无意义 |
| `object` | `floor(objectPos / s) * s` | **3D 默认**（ADR-010 §4bis-4 裁定） | 物体旋转/移动时像素跟随物体，**无爬行**；产生"体素感" | 透视下不与屏幕像素对齐 |
| `screen` | `floor(screenUV * res / s) * s` | 可选 | 与屏幕像素严格对齐 | 物体移动时像素"爬行"（每帧不同顶点落进不同格），视觉上非常明显 |

**用户裁定（ADR-010 §4bis-4）**：接受 3D 的"体素感"，2D 为像素风主场。因此 3D 默认 `object`，不做 `screen` 的默认路径（保留为 `style.parameters.gridSpace` 可选值）。

**体素感的强化（3D 专有）**：3D 下除坐标量化外，额外启用：

1. **顶点栅格吸附**：`sg_vdisp_grid_snap`（`TECH_FAMILY_SPEC_MATERIAL.md` §3.6），把网格顶点也吸到同一栅格 → 轮廓本身呈方块阶梯，而不只是表面纹理。
2. **法线量化**：`N = normalize(round(N / 0.5) * 0.5)`（量化到 6~26 个方向）→ 光照分成有限几档，读感像体素模型的面着色。仅 `SG_VfxLit` 主图。
3. **粒子/碎块位置吸附**：见 §3.6。

三者同时启用时读感是"体素"，只启用坐标量化时读感是"贴了像素纹理的平滑模型"——后者是失败态。谓词 PX-6 断言 3D 像素风产物三者齐备。

### 3.3 有限色带

```
// 1. 亮度量化到 colorSteps 级
L  = luminance(inColor)
Lq = floor(L * colorSteps) / max(colorSteps - 1, 1)
// 2. 映射到元素色板的离散色带（5 色，不插值）
idx   = round(Lq * 4)                        // 0..4 → residue/cool/primary/secondary/hot
color = paletteDiscrete(idx)
// 3. 可选：每通道再量化（更强的复古感）
if (perChannelQuant): color = floor(color * channelLevels) / channelLevels
```

`colorSteps` 默认 5（对应色板 5 色），范围 3~8。**注意与卡通的差异**：卡通量化的是"明暗档位"并保留色板的色相变化；像素量化的是"色带索引"，输出严格来自 5 个离散色，中间没有任何插值。

**色带扩展**：`colorSteps > 5` 时，超出的档位由相邻色板色的**固定中点**填充（编译期算好写入 `_PaletteMid0…2`），仍然是离散色，不是运行时插值。

### 3.4 硬边、alpha 二值化与 Bayer 抖动

```
// 阈值宽度强制为 0（编译期把上游 sg_threshold_* 换成 sg_threshold_hard，width=0.001）
// alpha 二值化：
a = step(alphaCutoff, inAlpha)
// 有序抖动（Bayer 4×4，在虚拟像素栅格上）：
if (ditherLevels > 0):
    cell   = floor(inPixelUv)                    // 虚拟像素坐标（已量化）
    bayer  = bayer4x4(cell.x % 4, cell.y % 4)    // ∈ [0,1)，16 阶
    q      = floor(inAlpha * ditherLevels) / ditherLevels
    frac_  = inAlpha * ditherLevels - floor(inAlpha * ditherLevels)
    a      = step(bayer, frac_) > 0 ? min(q + 1.0/ditherLevels, 1) : q
    a      = step(alphaCutoff, a)                 // 仍然二值输出
```

**Bayer 矩阵必须在虚拟像素栅格上采样，不是屏幕像素**：否则抖动图案的颗粒比"像素"更细，两种尺度打架，读感混乱。`inPixelUv` 由 `sg_flow_grid_snap` 输出，保证抖动格 = 像素格。

`bayer4x4` 用解析式实现（无查找表）：标准的 4×4 Bayer 矩阵可由位交错公式生成，编译期展开成常量分支。

**为什么不做半透明渐隐**：像素画的传统里没有 alpha 混合，透明度分级靠抖动图案。允许半透明会立刻暴露"这是 3D 渲染加了滤镜"。

### 3.5 时间量化（说明 ≠ 序列帧）

```
tq = floor(_LocalTime * frameRate) / frameRate      // sg_flow_time_quantize
```

`frameRate` 默认 12，范围 8~15。全部流动、阈值推进、闪烁的时间输入都用 `tq`。

**为什么这不是序列帧（逐条对照 ADR-010 §5 的禁止项）**：

| 序列帧的定义特征 | 时间量化 |
|---|---|
| 存在预先渲染/绘制的**帧图像资产**（sprite sheet / flipbook 纹理） | **零图像资产**。形态每帧仍由噪声/SDF 逐像素解析计算 |
| 帧数有限，超出后循环或停止 | 时间连续无界，量化只影响采样时刻。`tq` 在 `t=100s` 与 `t=0s` 处的形态由函数决定，不是查表 |
| 形态被烘死，参数无法改变形态 | 全部参数（`seed / scale / speed / 元素预设 / 风格`）仍然逐帧生效并改变形态 |
| 视角/朝向变化需要额外的帧集合 | 3D 下形态随视角连续变化（除了时间被量化） |
| 内存成本 = 帧数 × 分辨率 | 内存成本 = 0 |

**唯一的相似之处**：视觉刷新率是离散的。这是像素画的节奏感来源，也是它与"平滑动画"的区别。谓词 PX-5 断言：像素风产物中零个 `textureSheetAnimation`、零个 flipbook 配置——机器可查地证明这条纪律。

**时间量化与档位无关**（`STYLE_CATALOG_v1.md` §5）：它降低视觉刷新率但不降低任何成本（`floor` 是 1 条指令）。因此六档一致。

### 3.6 粒子 / 几何的栅格吸附

| 族 | 吸附什么 | 实现位置 | 参数 |
|---|---|---|---|
| GPU 粒子 | **渲染位置**（不是模拟位置） | VFX Graph 的 Output 块内、Orient 之前（`TECH_FAMILY_SPEC_PARTICLES.md` §6.1） | `PixelWorldSize` |
| CPU 粒子 | 粒子 **quad 的中心点**（不是逐顶点） | 粒子材质的顶点着色器（同上） | `PixelSnapSize` |
| 网格粒子 | 实例位置（同上）+ 网格自身顶点（`sg_vdisp_grid_snap`） | 两处 | 同 |
| 生成器网格 | 顶点（静态几何时可在编译期做，`TECH_FAMILY_SPEC_MESH_LIGHT.md` §5.4） | 编译期或 `sg_vdisp_grid_snap`，**二选一** | `quantizeVertices` / `pixelSize` |
| 预破碎碎块 | 碎块的位置（运行时刚体位置在渲染前吸附） | 碎块材质的顶点阶段（对象中心吸附） | 同 |
| Trail / Line | 顶点位置 | 材质顶点阶段 | 同；宽度量化到 1/2 格 |

**关键纪律：只量化渲染，不量化模拟**。物理与力场必须在连续空间求解，否则粒子会卡在格点上、刚体会抖动。

### 3.7 像素 × 5 技术族约束落点

| 族 | 约束 | 实现位置 |
|---|---|---|
| 材质 | 坐标栅格量化（上游）+ 色带 + alpha 二值化 + 抖动 + 时间量化 + 1 格描边 | `sg_flow_grid_snap` + `sg_flow_time_quantize`（上游）+ `sg_style_pixel`（下游）+ 编译期把 `sg_threshold_soft` 换成 `sg_threshold_hard` |
| GPU 粒子 | 形状 → 方块/十字/3×3 SDF；拉伸 → 短条（长度量化到整格，最长 4 格）；位置吸附；尺寸量化 1/2/3/4 格；旋转量化 90°；密度倍率 0.5；`ColorOverLife` 3~5 色阶跃 | Output 块 + 材质变体（`TECH_FAMILY_SPEC_PARTICLES.md` §6） |
| CPU 粒子 | 同上；`RotationOverLifetime` 无法量化 → 由材质顶点阶段量化 | 材质 + 模块字段 |
| 网格 | `maxSubdivision` 低；顶点位移量化到栅格；3D 可选 1 格宽描边壳；碎块数减半、块更大、位置吸附 | 生成器参数 + `sg_vdisp_grid_snap` |
| 局部光 | `intensitySteps = 4`；`flickerQuantize = true` 且按 `frameRate` 时间量化；**不允许阴影**；`range` 建议量化到格的整数倍 | `VfxLightBeat` 字段 |

### 3.8 像素 2D / 3D 差异

| 项 | 3D | 2D |
|---|---|---|
| 栅格空间 | 网格层 `object`（体素感）；quad/billboard 层 `uv` | 全部 `worldXY`（正交下所有层天然对齐，**像素风的最佳维度**） |
| 栅格对齐 | 不同层难以完全对齐（透视），接受 | 相同 `pixelWorldSize` → 完全对齐 |
| 体素强化 | 顶点吸附 + 法线量化 + 粒子位置吸附三者齐备（§3.2） | 无需（2D 无法线光照，无体积） |
| 光 | 连续 Point Light（妥协，`STYLE_CATALOG_v1.md` §3.5） | 连续 Light2D（妥协）；可用 `ground.cast` 材质层做量化"假光斑" |
| 排序 | 透明队列 | Sorting Layer；抖动透明与 Sorting 无冲突（抖动输出是二值 alpha，无混合顺序问题） |
| 描边 | 1 格宽法线外扩壳（可选）或材质 1px | 材质 1px |

### 3.9 像素风与 REFERENCE_ANALYSIS §2 的兼容

| 准则 | 像素风下如何成立 |
|---|---|
| §2-1 亮度分级 | **天然成立**（有限色带就是分级）。且 `sg_comp_hdr_grade` 的台阶倍率关系在色板离散化前已建立，HG-1 谓词通过 |
| §2-2 硬柔并存 | **需要刻意保留**：辉光层的 `falloffCurve` 在像素风下用 `step`（阶梯光圈）但 `falloffSteps` 取 4~5（比卡通多），并且 `layerCount ≥ 2`。得到的是"分成几圈的光晕"——这正是像素艺术里的辉光画法 |
| §2-3 形态各向异性 | 元素参数，风格不覆写 |
| §2-4 低亮雾幕 | **风险点**：`veil` 层的极低 alpha（0.02~0.2）在二值化后会整层消失。对策：`veil` 层强制启用抖动（`ditherLevels ≥ 2`），低 alpha 表现为稀疏的像素点阵 —— 这既保留了雾幕的体量提示，又符合像素画法。谓词 PX-7 断言 |
| §2-5 离散高亮点克制 | 天然契合（像素风粒子本就稀疏、每颗清晰） |
| §2-6 光晕溢出 | 靠色带中的亮色 + 自带辉光的阶梯光圈。`hdrClamp` 默认 1.0 但**不截断到 1.0**——`hot` 色本身 > 1，保证 Bloom 仍有溢出（HG-2 通过） |

---

## 4. 风格不覆盖元素的什么（边界表）

风格与元素的分界在 `STYLE_CATALOG_v1.md` §1.4 已给出原则。以下是实现层的**禁止覆写清单**——编译器如果发现风格约束试图写这些字段，是 bug：

| 字段类别 | 归属 | 风格可否覆写 |
|---|---|---|
| 噪声形态选择（哪个子图）、`aniso`、`flowDir`、`speedRatio` | 元素 | **否** |
| 色板 5 色的 RGB 值 | 元素 | 否（风格只能整体重映射饱和度/明度，见下） |
| 色板的 `saturationMul` / `valueCurve` / `hdrClamp` | **风格** | 是 |
| 阈值宽度的**基准值** | 元素 | 否 |
| 阈值宽度的**锐化系数** `edgeSharpness` | **风格** | 是（§2.3） |
| 粒子的力场与运动倾向 | 元素 | **否** |
| 粒子的基础形、尺寸量化、旋转量化、颜色模式 | **风格** | 是 |
| 粒子的发射率基准 | 元素 / recipe | 风格只给"建议倍率"，与档位上限取小（`STYLE_CATALOG_v1.md` §5） |
| 几何生成器选择与形态参数 | 元素 | **否** |
| 几何细分上限、位移量化档数、描边壳开关 | **风格** | 是 |
| 光的颜色/色温、`flickerMode`、`flickerRate`、`flickerDepth`、`decayShape` | 元素 | **否** |
| 光的 `intensitySteps`、`flickerQuantize`、`beatFrameRate`、`saturationMul`、`castShadows` | **风格** | 是 |
| 辉光层的 `anisotropy`、`innerColor`、`outerColor` | 元素 | 否（色来自色板，各向异性来自运动） |
| 辉光层的 `falloffCurve`、`layerCount`、`layerRatio`、`breakupNoise` | **风格** | 是（§5.3 默认值表） |
| 层结构、节拍、接口 | 原型 | **否**（风格零结构改动） |

谓词 ST-3 断言：编译报告中风格约束写入的字段集合 ⊆ 上表的"是"行。

---

## 5. StylePreset 默认值表

### 5.1 资产形状

沿用 `STYLE_CATALOG_v1.md` §1.3 的形状，补齐辉光与新增字段：

```
StylePreset (ScriptableObject 或 JSON 编译到 SO)
├─ id, displayName, version
├─ material:           { styleSubgraph, params{ …§5.2 } }
├─ upstreamOverrides:  { thresholdSharpen, detailMul, forceHardThreshold, gridSnapSpace, timeQuantizeFps }
├─ glowDefaults:       { …§5.3 的 18 项 }
├─ particleConstraints:{ shapeMap, sizeStep, minSize, rotationStep, trailWidthSteps, colorMode, densityMul, positionSnap }
├─ meshConstraints:    { maxSubdivision, minSubdivision, displacementSteps, outlineShell, vertexSnap, normalQuantize, fragmentCountMul }
├─ lightConstraints:   { intensitySteps, flickerQuantize, beatFrameRate, saturationMul, allowShadows, rangeQuantize }
├─ paletteRemap:       { saturationMul, midBoost, hueShift, hdrClamp, discreteBands }
└─ tierInteraction:    { …§6 的按档覆写 }
```

`upstreamOverrides` 是本文档新增的一节——它承载 §2.3 与 §3.1 指出的"风格需要改写上游子图属性"的那部分，使这个例外情形是**显式声明的数据**而不是编译器里的硬编码特例。

### 5.2 `material.params` 默认值

| 参数 | cartoon | pixel | none（写实基线） | 范围 |
|---|---|---|---|---|
| `shadingSteps` | **3** | — | — | 2~4 |
| `edgeSharpness` | **0.85** | 1.0（强制硬） | 0 | 0~1 |
| `outlineWidth` | **0.02**（solid）/ 0（volume） | 1 格 | 0 | 0~0.1 |
| `outlineColor` | `cool × 0.4` | `cool × 0.3` | — | — |
| `saturationMul` | **1.3** | **1.1** | 1.0 | 0.5~2 |
| `midBoost` | **0.25** | −0.15（压中间调） | 0 | −0.5~0.5 |
| `hdrClamp` | **1.5** | **1.0** | 8.0（不压） | 0.5~8 |
| `detailMul` | **0.5** | **0.4** | 1.0 | 0~1 |
| `innerLine` | false | false | false | — |
| `bandDither` | `1/(steps*24)`（仅 volume 层） | — | 0 | — |
| `aaScale` | **1.0** | 0（不做 fwidth 抗锯齿） | — | 0~2 |
| `pixelSize` | — | **0.0625**（2D，= 1/16 世界单位）/ 由包围盒推算（3D） | — | >0~1 |
| `colorSteps` | — | **5** | — | 3~8 |
| `perChannelQuant` | — | false | — | — |
| `alphaCutoff` | — | **0.5** | — | 0~1 |
| `ditherLevels` | — | **2**（MM+）/ 0（ML） | 0 | 0~4 |
| `frameRate` | — | **12** | 0（不量化） | 8~15 |
| `outline1px` | — | **true** | false | — |
| `gridSpace` | — | `worldXY`（2D）/ `object`（3D） | — | 4 值 |
| `normalQuantize` | false | **true**（3D） | false | — |

### 5.3 `glowDefaults`：辉光层 18 参数的风格默认值

REFERENCE_ANALYSIS §3bis 要求"由风格轴给默认值（卡通：阶梯衰减 + 少层；写实：高斯 + 多层）"。完整表：

| 参数 | cartoon | pixel | none（写实基线） | 说明 |
|---|---|---|---|---|
| `enabled` | 按层角色（材质族 §5.11） | 同 | 同 | 风格不改开关，只改画法 |
| `glowRadius` | **1.5** | **1.4** | **1.8** | 卡通/像素半径略小（硬光圈太大会糊） |
| `falloffCurve` | **`step`** | **`step`** | **`gaussian`** | 用户裁定的"卡通：阶梯衰减" |
| `falloffPower` | 1.0 | 1.0 | **1.5** | 仅 `linear` 型用 |
| `falloffSteps` | **3** | **4** | — | 像素多一档，配合 4 级色带 |
| `layerCount` | **2** | **2** | **3** | 用户裁定的"卡通：少层 / 写实：多层" |
| `layerRadiusRatio` | **1.7** | **1.6** | **1.55** | 卡通层间距更大（阶梯更分明） |
| `layerIntensityRatio` | **0.4** | **0.45** | **0.5** | |
| `breakupNoise` | **0.20** | **0.10** | **0.35** | 卡通/像素要更规则的光圈；但**不为 0**——完全规则的圆仍然是贴纸感（§5.5 的核心论点在任何风格下成立） |
| `breakupAngularFreq` | 4 | 3 | 5 | |
| `anisotropy` | 元素给 | 元素给 | 元素给 | 风格不覆写（§4） |
| `anisotropyAxisMode` | 元素/原型给 | 同 | 同 | |
| `innerColor` | `hot` | `hot`（离散） | `hot` | |
| `outerColor` | `primary` | `primary`（离散） | `primary` | |
| `colorMixPower` | **1.0**（线性混合，配合阶梯） | **0.8** | **1.4** | |
| `softDepthFade` | **0.4** | **0.3** | **0.5** | 像素风的软化会破坏硬边，取小 |
| `flickerCoupling` | **0.8** | **1.0** | **0.6** | 卡通/像素的光与晕应该完全同步跳档（分离会看出"两套动画"） |
| `glowShape` | `billboard` | `billboard` | `billboard`（PM+ 可 `domeBillboard`） | |

**像素风的辉光额外规则**：`Glow_k` 的材质同样过 `sg_style_pixel`，因此光圈本身也被色带量化与栅格吸附——得到的是"由大像素块构成的同心光圈"，而不是平滑的光晕加了个像素滤镜。

### 5.4 `particleConstraints` / `meshConstraints` / `lightConstraints` 默认值

| 字段 | cartoon | pixel | none |
|---|---|---|---|
| `shapeMap.circle` | `circleOutlined` | `square` | `circle` |
| `shapeMap.stretched` | `stretched`（≤3:1） | `shortBar`（≤4 格） | `stretched` |
| `shapeMap.mesh` | `lowPolyOutlined` | `voxelBlock` | `mesh` |
| `sizeStep` | 3 档 | 4 档（1/2/3/4 格） | 0（连续） |
| `minSize` | 0.08（相对） | 1 格 | 0.02 |
| `rotationStep` | 0（不量化） | 90° | 0 |
| `trailWidthSteps` | 3 | 2（1/2 格） | 0 |
| `colorMode` | `Fixed`（2~3 色） | `Fixed`（3~5 色） | `Blend` |
| `densityMul` | **0.6** | **0.5** | 1.0 |
| `positionSnap` | false | **true** | false |
| `maxSubdivision` | 中档（生成器六档表的 MH 列） | 低档（MM 列） | 档位上限 |
| `minSubdivision` | 生成器最低 | 生成器最低 | 生成器最低 |
| `displacementSteps` | **3** | 栅格量化 | 0（连续） |
| `outlineShell` | **true**（MH+）/ false（ML,MM） | 可选（默认 false） | false |
| `vertexSnap` | false | **true** | false |
| `normalQuantize` | false | **true**（3D） | false |
| `fragmentCountMul` | 1.0 | **0.5**（碎块减半、块更大） | 1.0 |
| `intensitySteps` | **3** | **4** | 0（连续） |
| `flickerQuantize` | **true** | **true** | false |
| `beatFrameRate` | 0 | **12** | 0 |
| `light.saturationMul` | **1.2** | **1.1** | 1.0 |
| `allowShadows` | **true**（PM/PH） | **false** | true（PM/PH） |
| `rangeQuantize` | 0 | 格的整数倍 | 0 |

---

## 6. 风格 × 六档交互表

| 风格约束 | ML | MM | MH | PL | PM | PH | 理由 |
|---|---|---|---|---|---|---|---|
| **卡通** | | | | | | | |
| 描边壳（3D solid 层） | **关**（材质暗边） | **关**（材质暗边） | 开 | 开 | 开 | 开 | ADR-010 §4bis-5；每网格层 +1 draw call |
| SDF 描边（2D / quad / 粒子） | 开 | 开 | 开 | 开 | 开 | 开 | 零额外 draw call |
| `shadingSteps` | 3 | 3 | 3 | 3 | 3 | 3 | shader 内 1~2 条指令，与档位无关 |
| `bandDither`（volume 层） | 关 | 开 | 开 | 开 | 开 | 开 | ML 省 1 次哈希 |
| `aaScale`（fwidth 抗锯齿） | 0（关） | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | ML 省 1 条 `fwidth` |
| `innerLine` | 关 | 关 | 可选 | 可选 | 可选 | 可选 | +1 条 `fwidth` |
| 辉光 `layerCount` | 1 | 2 | 2 | 2 | 2 | 3 | 与材质族 §10.3 的档位上限取小 |
| 光 `intensitySteps` | 3（`光:烘` 下作用于材质发光倍率） | 3 | 3 | 3 | 3 | 3 | 零成本 |
| 光阴影 | — | 关 | 关 | 关 | ≤1 盏 | ≤2 盏 | 档位表 |
| 粒子 `densityMul` 0.6 | 与档位上限取小 | 同 | 同 | 同 | 同 | 同 | `STYLE_CATALOG_v1.md` §5 |
| **像素** | | | | | | | |
| 坐标栅格量化 | 开 | 开 | 开 | 开 | 开 | 开 | 1~2 条指令，风格的身份 |
| 色带量化 | 开 | 开 | 开 | 开 | 开 | 开 | 同 |
| 时间量化 | 开 | 开 | 开 | 开 | 开 | 开 | 降低刷新率但不降成本（`STYLE_CATALOG_v1.md` §5） |
| `ditherLevels` | **0** | **2** | **2** | **2** | **4** | **4** | ML 关：抖动的 overdraw + 视觉噪在低端过重（`STYLE_CATALOG_v1.md` §3.7） |
| 1px 描边 | 开 | 开 | 开 | 开 | 开 | 开 | 零成本 |
| 顶点栅格吸附（3D） | 开 | 开 | 开 | 开 | 开 | 开 | 顶点阶段 1 条指令 |
| 法线量化（3D，`SG_VfxLit`） | 开 | 开 | 开 | 开 | 开 | 开 | 同 |
| 3D 描边壳（1 格宽） | 关 | 关 | 可选 | 可选 | 可选 | 可选 | 同卡通的成本理由 |
| 粒子位置吸附 | 开 | 开 | 开 | 开 | 开 | 开 | Output 前 1 条指令 |
| 碎块 `fragmentCountMul` 0.5 | 4（8×0.5） | 8 | 16 | 24 | 48 | 100 | 在档位块数上限之上再乘 0.5 |
| 光阴影 | — | 关 | 关 | 关 | **关** | **关** | 风格禁止（`STYLE_CATALOG_v1.md` §3.5），比档位更严 |
| 光 `beatFrameRate` 12 | 开 | 开 | 开 | 开 | 开 | 开 | 零成本 |
| 辉光 `layerCount` | 1 | 2 | 2 | 2 | 2 | 2 | 像素风不需要更多层（阶梯圈本就有限） |

**取小规则**：风格给的是**约束**，档位给的是**上限**，二者冲突时一律取更严的一方。唯一例外：风格的"禁止"（如像素禁阴影）无条件生效，即使档位允许。

---

## 7. 构造性谓词（EditMode 机器检查）

### 7.1 通用风格谓词（ST-*）

| 编号 | 谓词 |
|---|---|
| ST-1 | 每个产物材质恰好启用一个 `_STYLE_NONE / _STYLE_CARTOON / _STYLE_PIXEL` 关键字（材质族 MV-2 的复述，风格侧入口） |
| ST-2 | 产物的 `StyleStage` 子图引用与 recipe 的 `style.id` 一致（不允许"声明卡通但挂了别的子图"） |
| ST-3 | 编译报告中风格约束写入的字段集合 ⊆ §4 边界表的"是"行。断言方式：编译器为每次风格写入登记 `(field, source=style)`，测试比对该集合与常量白名单 |
| ST-4 | 风格叠加**零结构改动**：同一 recipe 分别以 `style=none` 与 `style=cartoon/pixel` 编译，两个产物的 GameObject 层次结构（名称与父子关系）差异**仅限**于 `Outline` 子节点的有无。层数、层名、组件类型集合（除描边壳的 MeshRenderer）必须完全一致 |
| ST-5 | 风格不改节拍与接口：两次编译的控制器阶段表与 `interface` 参数/事件表逐字段相同 |
| ST-6 | StylePreset 资产的 `id` ∈ schema 的 `styleId` 闭集；`version` 非空 |

### 7.2 卡通谓词（CT-*）

| 编号 | 谓词 |
|---|---|
| CT-1 | `shadingSteps ∈ [2,4]`；`_ShadingSteps` 属性在每个卡通材质上已赋值 |
| CT-2 | `inLayerKind == solid` 的层在 MH+ 档必有 `Outline` 子节点；在 ML/MM 档必**无** `Outline` 子节点且其材质的 `_DarkRimDarkness > 0`（**降级不等于什么都没有**，与局部光 LT-4 同一纪律） |
| CT-3 | `inLayerKind == volume` 的层 `outlineWidth == 0`（`STYLE_CATALOG_v1.md` §2.7） |
| CT-4 | `edgeSharpness` 与 `detailMul` 已写入上游子图属性：断言该层的 `sg_threshold_*.width` == `lerp(元素基准, 0.005, edgeSharpness)`（±1e-4），且噪声细节层权重 == 元素基准 × `detailMul`（§2.3 的编译期注入） |
| CT-5 | 卡通产物的 HG-1（相邻强度台阶亮度比 ≥ 2.0）仍然通过（§2.8-3：色阶量化不得压平强度台阶） |
| CT-6 | 卡通产物的 `veil` 类层（材质变体为 `mat_veil_soft`）的 `_ShadingSteps ≥ 4` 且 `_BandDither > 0`（§2.8-2） |
| CT-7 | 卡通产物的每个启用辉光的层，`falloffCurve == step` 且 `layerCount ≥ 2`（§2.8-1：柔的那一半必须保留） |
| CT-8 | `saturationMul ≤ 1.5`（§2.5 的色域上限） |

### 7.3 像素谓词（PX-*）

| 编号 | 谓词 |
|---|---|
| PX-1 | 每个像素风材质的 `sg_flow_grid_snap` 已接入且 `pixelSize > 0`（**坐标量化在上游**，§3.1）。断言方式：变体清单声明该层启用了 `gridSnap`，且材质 `_PixelSize > 0` |
| PX-2 | 每个像素风材质的上游阈值子图是 `sg_threshold_hard`（不是 `soft`），`width ≤ 0.002` |
| PX-3 | `snapSpace` 在 2D 产物中为 `worldXY`（或 recipe 显式覆写），在 3D 产物中为 `object`（或显式覆写）；不得为编译器默认之外的值而无 recipe 声明 |
| PX-4 | 同一 2D 产物内全部启用栅格的层的 `_PixelSize` 相同（栅格必须对齐，§3.8）。3D 不做此断言（透视下无法对齐，已接受） |
| PX-5 | **零序列帧**：像素风产物中零个 `ParticleSystem.textureSheetAnimation` 启用、零个 VFX 模板声明 `usesFlipbook`、零张纹理资产被材质引用（除 `sg_comp_palette_lut` 的可选 LUT）。这条是本风格最关键的纪律谓词（§3.5） |
| PX-6 | **3D 体素三件套齐备**：3D 像素风产物中，网格族层必须同时满足 (a) 顶点吸附启用（`sg_vdisp_grid_snap` 或生成器 `quantizeVertices`）、(b) `SG_VfxLit` 主图的层启用法线量化、(c) 粒子层启用位置吸附。缺任一项 FAIL（§3.2：只做坐标量化是失败态） |
| PX-7 | `veil` 类层的 `ditherLevels ≥ 2`（MM+ 档），否则低 alpha 雾幕会在二值化后整层消失（§3.9） |
| PX-8 | 全部 `VfxLightBeat.castShadows == false`（§3.7，比档位更严的风格禁止） |
| PX-9 | `frameRate ∈ [8,15]`；`_FrameRate` 已写入每个像素风材质 |
| PX-10 | 像素风产物的 HG-2（`hot` 台阶亮度 ≥ 1.5）仍然通过（§3.9：`hdrClamp=1.0` 不等于截断到 1.0） |

### 7.4 fail-closed 三路（风格侧具体化）

1. **未立法风格拒绝**：`style.id` 不在已实现的 `{none, cartoon, pixel}` 内 → `E205`。schema 的 `styleId` 枚举含 6 个候选风格（`realistic / stylized / inkwash / neon / holo / dark`），它们**在 schema 里合法但在编译器里被拒绝**——这是刻意的：schema 预留字段不等于实现存在。谓词 ST-6 断言 StylePreset 资产库恰好含 3 个 id。
2. **无 StylePreset 资产拒绝**：`style.id` 有效但对应 StylePreset 资产缺失 → `E205`。
3. **风格越权拒绝**：风格试图写 §4 边界表的"否"行 → ST-3 FAIL（这是编译器 bug 的检测，不是用户错误）。

显式豁免机制与其他族同构（测试内常量表 / 豁免仍跑谓词 / 已消费集合恰等于声明清单）。

---

## 8. 审计清单

- [ ] 卡通：色阶分层（3 条色带对策）、描边三路（壳 / 暗边 / SDF）与 ADR-010 §4bis-5 的低端档替代、锐利阈值的编译期注入机制、高饱和、HDR 压台阶、内描线。
- [ ] 卡通与 REFERENCE_ANALYSIS §2「硬柔并存」的 4 条兼容措施。
- [ ] 像素：坐标量化在上游 / 颜色量化在下游的两半结构、4 种栅格空间与 3D 体素感三件套（ADR-010 §4bis-4）、有限色带、alpha 二值化 + Bayer 抖动（在虚拟像素格上）、时间量化 ≠ 序列帧的 6 条对照、粒子/几何 6 处栅格吸附。
- [ ] 像素与 REFERENCE_ANALYSIS §2 六条准则的兼容（含 `veil` 层的抖动救济）。
- [ ] 两风格的 StylePreset 默认值表：material 参数 20 项 + **辉光 18 项** + 粒子/网格/光约束 21 项。
- [ ] 风格 × 六档交互表（卡通 10 行 + 像素 12 行）与"取小规则 + 风格禁止无条件生效"。
- [ ] 风格不覆盖元素的边界表（16 行）。
- [ ] 构造性谓词 24 条（ST 6 / CT 8 / PX 10）+ fail-closed 三路。
- [ ] 无低分辨率 RT、无相机/后处理 pass、无序列帧（PX-5 机器断言）。
- [ ] 风格零结构改动（ST-4 / ST-5 机器断言）。
- [ ] 正文无具体特效名。
