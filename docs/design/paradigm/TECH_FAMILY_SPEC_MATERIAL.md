# 技术族实现规格：程序化材质族（Procedural Material Family / Shader Graph）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）、`docs/design/references/REFERENCE_ANALYSIS.md`（质感标尺）
配套文档：`PROTOTYPE_CATALOG_v1.md`（层角色词表 §2.1 / 六档基准预算 §2.4）、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`TECH_FAMILY_SPEC_PARTICLES.md`、`TECH_FAMILY_SPEC_MESH_LIGHT.md`、`STYLE_IMPL_CARTOON_PIXEL.md`、`COMPILER_BOUNDARY_V2.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **技术族轴** | 5 族中的第 1 族（程序化材质 `material`）**全部实现规格**：4 张主图 + **53 个 Shader Graph 子图**（噪声 7 / SDF 形状 9 / 阈值 5 / 边缘 5 / 流动 7 / 顶点位移 8 / 合成 9 / 风格插槽 3）、`StyleStage` 插槽契约、辉光层 8 参数逐个实现方式、折射可选层双路实现与运行时检测、HDR-线性空间纪律、每子图的构造性谓词 |
| **原型轴** | 不新增、不修改原型；本族服务 21 个层角色中的 **18 个**（`core / body / edge / trail / ground / decal / flash / shock / beam_column / link / mesh_shell / surface / veil / column / fill / frame / debris（断面）/ orbit（材质部分）`）；`emission / light / cloth` 三个角色的主体由粒子族 / 局部光族 / 网格族承担（见配套文档），但它们的**渲染材质**同样由本族的子图组合而成 |
| **元素轴** | 13 元素 + `none` 的 5 个物理子画像在材质族的预设倾向，全部有可落地的子图组合（§9 元素→子图组合速查表） |
| **风格轴** | 首批 2（卡通 / 像素）的叠加机制在本文档定义为 `StyleStage` 插槽契约（§4）；两风格子图内部实现见 `STYLE_IMPL_CARTOON_PIXEL.md` |
| **维度轴** | 每个子图给出 2D / 3D 差异列；`_DIM_2D` / `_DIM_3D` 为编译期静态分支关键字 |
| **档位轴** | 六档全部有降级机制：每个子图声明 `minTier` 与 `sampleCost`，编译器按 `PROTOTYPE_CATALOG_v1.md` §2.4 的"材质噪声采样层数"预算截断（§10 降级规格） |

**纪律**：本文档正文只用"层角色 / 技术族 / 参数"表述，不出现任何具体特效名；参考图（`docs/design/references/*.png`）是质感标尺，本文档从中提炼的是**跨全部 58 原型有效的技术特征**，不是任何一张图的复刻规格。

---

## 1. 本族的职责边界与产物形状

### 1.1 职责

程序化材质族回答一个问题：**一个层的形态如何被算出来，而不是被贴出来**。ADR-010 §5 禁止序列帧，因此这一族是替代序列帧的核心。它承担：

- 形态生成（噪声 / SDF / 二者的阈值组合）
- 形态演化（流动 / 阈值推进 / 生长 / 消散）
- 边缘性格（软阈值 / 硬阈值 / 菲涅尔 / 描边 / 双阈值）
- 体量表达（低亮雾幕、多层加法辉光）
- 顶点级形变（与网格族分工见 `TECH_FAMILY_SPEC_MESH_LIGHT.md` §5）
- 颜色分级（线性 HDR 色板 → 强度台阶 → 风格量化）

### 1.2 产物形状

一个 `technique.family = material` 的层编译为：

```
Layers/<layerId>                       GameObject
├─ MeshRenderer + MeshFilter           3D：quad / 生成网格 / 基础几何
│  或 SpriteRenderer                    2D：quad 类层（sorting 显式）
│  或 CanvasRenderer + Graphic          F 类（UGUI 目标）
├─ Material（编译期克隆到产物目录）      Shader = 主图（§2）
└─ Glow_0 … Glow_{n-1}                  可选：辉光层子节点（§5），n = glowLayerCount
```

**一个层 = 一张材质 = 一个主图实例**。主图不是每个变体一张：**全族共用 4 张主图**（§2.2），形态差异由主图内挂载的子图组合 + 编译期静态关键字决定。这使 `COMPILER_BOUNDARY_V2.md` §3 的资产白名单是一个**小闭集**（4 主图 + 38 子图），而不是随变体增长的开集。

### 1.3 `technique.variant` 与主图的关系

`variant` 不是"一张 shader"，而是**主图 + 一组静态关键字 + 一组子图槽位选择 + 参数面**的具名组合，记录在变体清单资产（`VariantManifest`，见 `COMPILER_BOUNDARY_V2.md` §3.3）里。变体清单是编译器的输入，不是运行时资产。材质族的变体清单见 §8。

---

## 2. 主图（master graph）闭集

### 2.1 为什么是 4 张而不是 N 张

Shader Graph 的主图决定的是**渲染管线接线**（Surface Type、Blend、Cull、渲染目标、是否有顶点阶段、是否 UGUI 目标），这些是有限的正交组合；形态则完全由子图承担。把形态差异塞进主图会导致 shader 资产爆炸并使白名单失控。

### 2.2 主图清单

| id | URP Target | Surface / Blend | Cull | 顶点阶段 | 服务层角色 | 备注 |
|---|---|---|---|---|---|---|
| `SG_VfxSurface` | Universal / Unlit | Transparent / Alpha 或 Premultiply（关键字切换） | Back（可切 Off） | 有（接 `sg_vdisp_*`） | `core / body / edge / trail / ground / decal / shock / beam_column / link / mesh_shell / surface / veil / column / orbit / debris` | 本族主力主图。含 `_BLEND_ALPHA / _BLEND_PREMUL / _BLEND_MULTIPLY` 三关键字（`_BLEND_MULTIPLY` 服务"吸光"语义的暗核层） |
| `SG_VfxAdditive` | Universal / Unlit | Transparent / Additive | Off | 无 | `flash / edge.glow / body.halo / 辉光子节点` | 加法叠加专用；无顶点阶段以保证辉光片的成本下限；ZWrite Off、ZTest LEqual |
| `SG_VfxLit` | Universal / Lit（Simple Lighting） | Opaque 或 Transparent | Back | 有 | `mesh_shell / debris / core`（实体几何且需要接收局部光的场合） | 唯一接收 URP 光照的主图；卡通 cel 明暗（`STYLE_IMPL_CARTOON_PIXEL.md` §2.2）只在此主图上成立 |
| `SG_VfxCanvas` | Universal / Canvas（UGUI 目标） | Transparent / Alpha | Off | 无 | `fill / frame` | F 类专用；不含 Depth 相关节点（Canvas 下不可靠）；`softDepthFade` 在此主图恒为关（§5.7） |

### 2.3 主图统一端口契约

四张主图内部结构一致，都是同一条流水线：

```
[几何/UV 输入] → Space  → Flow  → Shape  → Threshold → Color → StyleStage → [输出]
                  §3.5     §3.5    §3.1/2    §3.3       §7        §4
                                                ↘ Edge §3.4 ↗
[顶点输入] → Vertex Displace §3.6 → [位置/法线输出]（仅 SG_VfxSurface / SG_VfxLit）
```

每个阶段是一个**子图槽位**（subgraph slot）。编译器按变体清单把指定子图接进槽位，未指定的槽位接入直通子图（`sg_passthrough_*`，零成本）。主图的槽位数与顺序**固定不可变**，这是白名单与谓词能成立的前提。

### 2.4 全族统一材质属性（所有主图必须暴露）

| 属性名 | 类型 | 来源 | 语义 |
|---|---|---|---|
| `_PaletteA` … `_PaletteE` | Color（HDR，线性） | 元素色板 `primary/secondary/hot/cool/residue` | §7.1 |
| `_Intensity` | Float 0~2 | 标准参数 `intensity` | 全局亮度 / 发射率倍率 |
| `_Scale` | Float 0.25~4 | 标准参数 `scale` | 形态空间尺度倍率（不是 transform 缩放） |
| `_Speed` | Float 0.25~3 | 标准参数 `speed` | 时间倍率，喂给全部流动 / 阈值推进 |
| `_Seed` | Float | 标准参数 `seed` | 噪声与随机相位偏移；同一预制体内各层用 `seed + layerOrdinal`（编译期烘死） |
| `_Progress` | Float 0~1 | 控制器阶段进度 | 阈值推进 / 生长 / 消散的统一驱动 |
| `_Phase` | Float | 控制器 | 当前阶段编号（0=launch,1=travel/sustain,2=impact,3=end），供阶段相关分支 |
| `_BeatValue` | Float 0~1 | 局部光节拍器（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §6） | 与光同呼吸的耦合输入 |
| `_LocalTime` | Float | 控制器 | 层局部时间（已乘 `_Speed`，已做风格时间量化） |

**纪律**：材质**不得**自行读 `Time` 节点作为主时间源。全部时间来自 `_LocalTime`。理由三条：(a) 池化复位后必须能从 0 重放；(b) 风格轴的时间量化（像素风）必须在一处生效；(c) 确定性截帧（画廊对比、`GALLERY_SPEC.md` §6）要求同一帧同一形态。允许的例外：`sg_flow_*` 内部用 `_LocalTime` 派生的相位，不是新时间源。

---

## 3. 子图库清单

**总览（53 个）**：噪声 7（§3.1）· SDF 形状 9（§3.2）· 阈值 5（§3.3）· 边缘 5（§3.4）· 流动 7（§3.5）· 顶点位移 8（§3.6）· 合成 9 + 直通 4（§3.7）· 风格插槽 3（§4.3）。其中 `sg_comp_glow_stack`（§5）与 `sg_comp_refract`（§6）在合成族计数内，其规格单列成节。

### 3.0 读表约定

- **端口**：`in:` 输入端口（类型），`out:` 输出端口（类型）。`f` = Float，`f2/f3/f4` = Vector2/3/4，`b` = Boolean（静态关键字），`i` = Int（静态）。
- **服务层角色**：取自 `PROTOTYPE_CATALOG_v1.md` §2.1 词表。
- **`sampleCost`**：进成本模型的采样计数（1 次噪声 / SDF 求值 = 1）。编译器把一个层内全部子图的 `sampleCost` 求和，对照 §2.4 的"材质噪声采样层数 / 张"上限。`sampleCost` 与参数相关时写成公式。
- **`minTier`**：最低可用档位（`ML < MM < MH ≈ PL < PM < PH`）。低于该档编译器必须替换为降级子图（§10）。
- **2D-3D 差异**：写"同"表示两维实现一致。

### 3.1 噪声族（noise）

| id | 端口 | 参数 | 用途（服务层角色） | 2D-3D 差异 | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_noise_fbm_aniso` | in: `p(f3)`, `time(f)`, `seed(f)` ／ out: `n(f)`, `grad(f3)` | `octaves i 1~3`、`baseFreq f`、`lacunarity f 1.6~2.4`、`gain f 0.4~0.6`、`aniso f2`（各向异性缩放：沿主轴压低频、垂直轴提高频）、`flowDir f3`、`speedRatio f`（层间速度比，第 k 层速度 = `speed * speedRatio^k`） | `core / body / trail / veil / surface / decal / ground` 的主形态。**这是 REFERENCE_ANALYSIS §2-3「形态各向异性」的落点**：`aniso` 不是可选装饰，元素预设必须给非 1 值，否则回到团块感 | 3D：`p` 取物体空间三维坐标，用 3D value noise + 三线性插值；2D：`p.xy`，用 2D value noise。同一子图内以 `_DIM_3D` 静态分支切换 | `octaves` / `MM`（`octaves=1` 时 `ML`） |
| `sg_noise_voronoi_cell` | in: `p(f3)`, `seed(f)` ／ out: `f1(f)`（到最近特征点距离）, `f2(f)`（次近）, `cellId(f)`, `cellCenter(f3)` | `density f`、`jitter f 0~1`、`metric i`（欧氏 / 曼哈顿 / 切比雪夫——后两者出直角晶格感） | 晶格形态（`core / mesh_shell / edge`）、块状破碎断面（`debris`）、`sg_threshold_grow` 的蔓延单元、`decal` 的结晶蔓延 | 3D：3D 单元（27 邻域搜索）；2D：2D 单元（9 邻域）。3D 版 `sampleCost` 计 2 | 2（3D）/ 1（2D）／ `MM` |
| `sg_noise_jagged_1d` | in: `u(f)`（沿线参数）, `time(f)`, `seed(f)` ／ out: `offset(f)`, `branchMask(f)` | `segments i 4~32`、`amplitude f`、`rephaseRate f 0~30`（相位跳变频率，`floor(time*rate)` 采样，**不插值**）、`branchDepth i 0~3`、`branchProbability f` | 折线 / 分叉形态的**材质侧**（`link / beam_column / trail / edge`）；与网格生成器 `jagged_polyline`（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4）配对使用：网格给拓扑，材质给逐帧抖动 | 同（一维参数化） | 1 ／ `ML` |
| `sg_noise_curl` | in: `p(f3)`, `time(f)`, `seed(f)` ／ out: `v(f3)` | `freq f`、`amplitude f`、`octaves i 1~2` | 无散度扰动场：`body` 的翻腾、`trail` 的湍流 UV 扰动、`veil` 的飘动。**材质侧只用于 UV 扰动**；粒子的力场版本见 `TECH_FAMILY_SPEC_PARTICLES.md` §3 | 3D：3 分量偏导；2D：单标量势的旋度（2 分量），成本减半 | 3（3D）/ 2（2D）／ `MH`（`PL`）；`MM` 只允许 `octaves=1` 的 2D 版 |
| `sg_noise_bubble_field` | in: `p(f3)`, `time(f)`, `seed(f)` ／ out: `holes(f)`, `bulge(f)` | `density f`、`riseSpeed f`、`popThreshold f`、`sizeRange f2` | 气泡 / 孔洞形态（`core / body / decal / surface`）：单元内圆 SDF 随时间沿 `+Y` 上浮，越过 `popThreshold` 后在 1~2 帧内半径归零（瞬变，不是渐隐） | 3D：球；2D：圆。共用 `sg_noise_voronoi_cell` 的单元划分 | 1 + Voronoi 成本 ／ `MM` |
| `sg_noise_stripe_polar` | in: `uvPolar(f2)`（`sg_flow_polar` 输出）, `time(f)`, `seed(f)` ／ out: `n(f)` | `stripeCount i`、`twist f`（角度随半径偏移量）、`softness f` | 螺旋条纹（`body / column / veil / trail` 的旋转丝带读感） | 同（依赖极坐标输入） | 1 ／ `ML` |
| `sg_noise_value_1d` | in: `x(f)`, `seed(f)` ／ out: `n(f)` | `freq f`、`quantize b`（是否 `floor` 采样） | 一维随机：宽度曲线抖动、逐段随机、`emission` 材质的闪烁相位 | 同 | 0（视为常数级）／ `ML` |

**噪声族纪律**：任何噪声子图**不得**采样纹理。ADR-010 §5 允许外部出图作"噪声种子"，其含义是离线生成一张**梯度查找表**注入 `sg_comp_palette_lut`，不是把噪声本身换成贴图。噪声一律解析计算（value/gradient noise 的哈希实现，`seed` 进哈希）。

### 3.2 SDF 形状族（sdf）

| id | 端口 | 参数 | 用途（服务层角色） | 2D-3D 差异 | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_sdf_primitive_2d` | in: `uv(f2)` ／ out: `d(f)`（带符号距离）, `grad(f2)` | `shape i`（`circle / ring / star / polygon / sector / roundedRect / leaf / capsule`）、`param f4`（按 shape 语义：半径 / 内外半径 / 角数与凹凸比 / 边数与圆角 / 起止角 / 半宽半高与圆角 / 叶宽与尖锐度 / 两端点与半径）、`rotation f` | 全部 quad 类层的形状底座：`core / flash / shock / ground / fill / frame / decal / orbit` | 2D 主场；3D 中用于 billboard/quad 层与贴地 quad | 1 ／ `ML` |
| `sg_sdf_primitive_3d` | in: `p(f3)` ／ out: `d(f)`, `grad(f3)` | `shape i`（`sphere / box / torus / cone / capsule / plane`）、`param f4`、`rotation f3` | 体积层的解析形状（`body / core / mesh_shell` 的体积近似）、`sg_comp_smoothmin` 的输入 | 仅 3D；2D 编译期替换为 `sg_sdf_primitive_2d` 对应形状 | 1 ／ `MH`（`PL`） |
| `sg_sdf_radial_burst` | in: `uv(f2)` 或 `dir(f3)` ／ out: `d(f)`, `rayId(f)`, `rayU(f)` | `count i 4~64`、`lengthRange f2`、`widthRange f2`、`angleJitter f 0~1`、`taper f 0~1`（锥度：根粗梢细）、`seed f` | **放射光针的材质侧**（ADR-010 §4bis-8）。服务 `flash / shock / edge / ground`。与网格生成器 `radial_spike_array`（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.19）的分工：网格版给真实几何与深度遮挡，材质版给零几何成本的低档替代 | 3D：`dir` 为球面方向（用球坐标角度分格）；2D：`uv` 极角分格 | 1 ／ `ML` |
| `sg_sdf_rune_ring` | in: `uv(f2)`, `seed(f)` ／ out: `d(f)`, `ringMask(f)`, `glyphMask(f)` | `ringCount i 1~4`、`ringRadii f4`、`tickCount i`、`glyphCount i`、`glyphComplexity i 1~3`、`spinRate f4`（各环独立，可反向） | 几何符号阵（`ground / mesh_shell / orbit / frame / core`）。字形由 `seed` 驱动的**程序化线段组合**（每字形 = 3~7 段直线/圆弧的布尔并），不是贴图字库 | 3D：多环各自倾斜（法线由 `ringTilt` 给），可在球面 UV 上展开；2D：同心椭圆 | 2 ／ `MM` |
| `sg_sdf_sacred_pattern` | in: `uv(f2)`, `seed(f)` ／ out: `d(f)` | `symmetry i 3~12`、`layerCount i 1~3`、`motif i`（`polygon / star / petal / crossRay`）、`radiusRatio f3` | 对称几何阵（`ground / flash / frame / veil`）。与 `sg_sdf_rune_ring` 的区别：本子图是**严格 N 次旋转对称**的洁净几何，无随机字形 | 同 | 1 ／ `ML` |
| `sg_sdf_grid` | in: `uv(f2)` 或 `p(f3)` ／ out: `d(f)`, `cellId(f2)`, `cellUv(f2)` | `gridType i`（`square / hex / tri`）、`cellSize f`、`lineWidth f`、`bevel f` | 栅格形态（`mesh_shell / body / fill / frame / surface`）；`cellId` 输出供"逐格点亮 / 逐格脱落"的阈值推进使用 | 3D：三平面投影（tri-planar）避免 UV 接缝，成本 ×3；2D：直接 UV | 1（2D）/ 3（3D 三平面）／ `ML`（2D）/ `MH`（3D） |
| `sg_sdf_scanline` | in: `uv(f2)`, `time(f)` ／ out: `mask(f)`, `sweepEdge(f)` | `lineCount f`、`dutyCycle f 0~1`、`sweepSpeed f`、`sweepWidth f`、`stepRate f`（阶跃频率，0=连续） | 扫描线 / 扫掠边（`fill / frame / body / mesh_shell / veil / surface`） | 同 | 0 ／ `ML` |
| `sg_sdf_crack_branch` | in: `uv(f2)` 或 `p(f3)`, `seed(f)` ／ out: `d(f)`, `branchOrder(f)`, `growthParam(f)` | `seedCount i 1~8`、`branchDepth i 1~4`、`angleSpread f`、`lengthDecay f 0.4~0.8`、`width f` | 裂纹 / 树状分叉（`decal / mesh_shell.crack / ground / surface / edge.fracture`）。实现：基于 `sg_noise_voronoi_cell` 的单元图，取相邻单元中心的连线子集构成分叉树，`branchOrder` 记录深度，`growthParam` 是沿树的归一化弧长——供阈值推进做"裂纹生长" | 3D：三平面或直接三维单元；2D：二维单元 | 2 + Voronoi ／ `MM` |
| `sg_sdf_leaf_petal` | in: `uv(f2)`, `seed(f)` ／ out: `d(f)`, `veinMask(f)` | `width f`、`tipSharpness f`、`veinCount i`、`curvature f` | 有机片状形（`emission` 的粒子材质、`orbit`、`body`）。`veinMask` 是叶脉细线 | 同 | 1 ／ `ML` |

### 3.3 阈值族（threshold）

| id | 端口 | 参数 | 用途 | 2D-3D | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_threshold_soft` | in: `field(f)`, `t(f)` ／ out: `a(f)`, `edgeDist(f)` | `width f 0.01~0.5`、`gamma f` | 软边（柔性形态的通用阈值）。`edgeDist` 输出给边缘族与描边 | 同 | 0 ／ `ML` |
| `sg_threshold_hard` | in: `field(f)`, `t(f)` ／ out: `a(f)`, `edgeDist(f)` | `width f 0.001~0.02` | 硬边（晶格 / 栅格 / 像素风） | 同 | 0 ／ `ML` |
| `sg_threshold_grow` | in: `field(f)`, `cellId(f)`, `cellCenter(f3)`, `progress(f)`, `origin(f3)` ／ out: `a(f)`, `frontDist(f)`, `cellPhase(f)` | `mode i`（`radial / cellular / directional / multiSeed`）、`frontWidth f`、`randomDelay f 0~1`（逐单元随机延迟量） | **蔓延 / 生长 / 消散的统一实现**：`mesh_shell.crack / decal / body.coverage / surface / core`。`cellular` 模式按 `cellCenter` 到 `origin` 的距离排序逐格点亮，`randomDelay` 打破整齐推进 | 同（`origin` 维度随 `_DIM_*`） | 0（复用上游采样）／ `ML` |
| `sg_threshold_dither` | in: `a(f)`, `screenOrPixelUv(f2)` ／ out: `aBinary(f)` | `levels i 0~4`、`cutoff f` | 有序抖动（Bayer 4×4）二值化。像素风的透明分级，也可作低档 alpha-blend 的替代 | 同（栅格空间由风格给） | 0 ／ `ML` |
| `sg_threshold_dual` | in: `field(f)`, `t(f)` ／ out: `core(f)`, `halo(f)` | `coreWidth f 0.001~0.02`、`haloWidth f 0.1~0.6`、`haloGain f` | **双阈值：极窄硬核 + 宽柔晕**。这是 REFERENCE_ANALYSIS §2-2「硬柔并存」在阈值级的直接落点，服务 `link / beam_column / core / edge / flash` | 同 | 0 ／ `ML` |

### 3.4 边缘族（edge）

| id | 端口 | 参数 | 用途 | 2D-3D | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_edge_fresnel` | in: `normalWS(f3)`, `viewWS(f3)` ／ out: `f(f)` | `power f 0.5~8`、`bias f`、`invert b` | 3D 实体 / 壳体的边缘光（`mesh_shell / core / edge / debris / column`） | 仅 3D。2D 编译期替换为 `sg_edge_sdf_rim` | 0 ／ `ML` |
| `sg_edge_sdf_rim` | in: `edgeDist(f)` ／ out: `f(f)` | `width f`、`power f`、`inner b`（内描 / 外描） | 2D 的"伪菲涅尔"：用到形状边的距离代替视角项；也用于 quad 类层在 3D 场景中的边缘光 | 2D 主场；3D quad 类层同用 | 0 ／ `ML` |
| `sg_edge_thickness` | in: `p(f3)`, `normalWS(f3)`, `viewWS(f3)`, `sdf(f)` ／ out: `thick(f)` | `maxThickness f`、`densityCurve i` | 解析厚度近似（沿视线求解析 SDF 的进出点距离），给半透明体的内散射与"越厚越亮/越暗"（`body / mesh_shell / core`）。**替代真实体积步进**，成本恒定 | 仅 3D（依赖 `sg_sdf_primitive_3d`）；2D 用 `1 - edgeDist` 近似 | 1 ／ `PL` |
| `sg_edge_outline_sdf` | in: `edgeDist(f)` ／ out: `outline(f)` | `width f`、`quantizeToPixel b` | 材质侧描边（2D 全部层、3D 的 quad 类层、低端档卡通的暗边替代）。**低端档卡通取消真描边壳后由本子图承担**（ADR-010 §4bis-5） | 同 | 0 ／ `ML` |
| `sg_edge_dark_rim` | in: `fresnelOrRim(f)`, `baseColor(f3)` ／ out: `color(f3)` | `darkness f 0~1`、`width f`、`hueShift f` | 材质暗边：把边缘区域的颜色**乘暗**而不是叠一条描边线。这是 ML/MM 档卡通描边壳的替代实现（`STYLE_IMPL_CARTOON_PIXEL.md` §2.3-B） | 同 | 0 ／ `ML` |

### 3.5 流动族（flow）

| id | 端口 | 参数 | 用途 | 2D-3D | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_flow_uv_scroll` | in: `uv(f2)`, `time(f)` ／ out: `uv(f2)` | `dir f2`、`speed f`、`tiling f2`、`layerIndex i`（决定 `speedRatio^k`） | 基础 UV 流动，全部平面层 | 同 | 0 ／ `ML` |
| `sg_flow_polar` | in: `uv(f2)`, `center(f2)`, `time(f)` ／ out: `uvPolar(f2)`（`r, theta`）, `r(f)`, `theta(f)` | `radialSpeed f`、`angularSpeed f`、`radialTiling f`、`angularTiling f` | 极坐标：环 / 阵 / 涡旋 / 放射（`ground / shock / body / column / flash / frame`）。2D 用极坐标 UV 代替环面网格是 `PROTOTYPE_CATALOG_v1.md` §2.5 的通则 | 同 | 0 ／ `ML` |
| `sg_flow_along_axis` | in: `p(f3)`, `axis(f3)`, `time(f)` ／ out: `u(f)`, `perp(f2)` | `speed f`、`tiling f`、`worldSpace b` | 沿轴流动 + 轴向-垂直分解：给 `sg_noise_fbm_aniso` 提供各向异性坐标（沿轴低频、垂直高频），服务 `trail / beam_column / link / column` | 3D：任意轴；2D：轴限于 XY 平面 | 0 ／ `ML` |
| `sg_flow_parallax` | in: `uv(f2)`, `viewTS(f3)` ／ out: `uv(f2)` | `depth f 0~0.2`、`steps i 1~4` | 视差偏移，给平面层一点"厚度"读感（`body / veil / surface / mesh_shell`）。**不是视差遮蔽映射**（无贴图可采样，`steps` 只对解析场生效） | 仅 3D；2D 无视角变化，编译期直通 | `steps` ／ `PM` |
| `sg_flow_curl_warp` | in: `uv(f2)` 或 `p(f3)`, `time(f)` ／ out: 同类型 | `strength f`、`freq f` | 用 `sg_noise_curl` 的输出扭曲坐标（翻腾 / 飘动） | 随维度 | 复用 curl 成本 ／ `MH`（`PL`） |
| `sg_flow_time_quantize` | in: `time(f)` ／ out: `time(f)` | `frameRate f 0~30`（0=不量化） | 时间量化（像素风的低帧率跳动 / 雷式相位跳变）。**说明**：这不是序列帧——形态仍逐像素解析计算，只是采样时刻被量化（`STYLE_IMPL_CARTOON_PIXEL.md` §3.5） | 同 | 0 ／ `ML` |
| `sg_flow_grid_snap` | in: `uv(f2)` 或 `p(f3)` ／ out: 同类型, `pixelUv(f2)` | `pixelSize f`、`space i`（`uv / object / worldXY / screen`） | 坐标栅格吸附（像素风的核心）。输出的 `pixelUv` 供 `sg_threshold_dither` 使用 | 3D 默认 `object`（体素感，ADR-010 §4bis-4）；2D 默认 `worldXY` | 0 ／ `ML` |

### 3.6 顶点位移族（vertex displacement）

只在 `SG_VfxSurface` / `SG_VfxLit` 的顶点阶段可用；`SG_VfxAdditive` / `SG_VfxCanvas` 编译期拒绝（`E203`）。

| id | 端口 | 参数 | 用途 | 2D-3D | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_vdisp_normal_noise` | in: `posOS(f3)`, `normalOS(f3)`, `time(f)` ／ out: `posOS(f3)`, `normalOS(f3)` | `amplitude f`、`freq f`、`speed f`、`maskByUv b` | 沿法线的噪声起伏（`core / mesh_shell / body / surface`）。法线用中心差分重算（3 次额外噪声采样，可关） | 同 | 1（+3 若重算法线）／ `MM` |
| `sg_vdisp_axial_stretch` | in: `posOS(f3)`, `axis(f3)`, `time(f)` ／ out: `posOS(f3)` | `stretch f`、`taper f`、`tipJitter f`、`profile i`（`linear / ease / tongue`） | 沿轴拉伸 + 梢部抖动（`core / column / trail / link`）：撕裂舌形、拉伸液滴、锥形柱 | 3D：真轴；2D：XY 内轴 | 0 ／ `ML` |
| `sg_vdisp_bulge_pulse` | in: `posOS(f3)`, `normalOS(f3)`, `time(f)`, `seed(f)` ／ out: `posOS(f3)` | `count i`、`amplitude f`、`riseTime f`、`popTime f`、`sizeRange f2` | 局部鼓泡：隆起后**瞬间**回落（不是正弦），服务 `core / body / surface / mesh_shell` | 同 | 1 ／ `MM` |
| `sg_vdisp_sway_bend` | in: `posOS(f3)`, `time(f)` ／ out: `posOS(f3)` | `axis f3`、`amplitude f`、`freq f`、`phaseByHeight f`、`heightPower f 1~3` | 摆动 / 弯曲（`column / veil / link / cloth（低档替代）/ mesh_shell.cage`）：幅度随高度幂次增长 | 同 | 0 ／ `ML` |
| `sg_vdisp_gerstner` | in: `posOS(f3)`, `time(f)` ／ out: `posOS(f3)`, `normalOS(f3)` | `waveCount i 1~4`、`amplitude f4`、`wavelength f4`、`steepness f4`、`dir f4`（打包 2 个方向） | 表面波（`surface / veil / cloth 的低档替代`） | 3D：XZ 平面波；2D：沿 X 的一维波 | `waveCount` ／ `MM` |
| `sg_vdisp_tension_jitter` | in: `posOS(f3)`, `time(f)`, `seed(f)` ／ out: `posOS(f3)` | `amplitude f`、`rate f`、`quantize b` | 张力抖动 / 高频颤动（`core / link / edge`）；`quantize` 时用 `floor(time*rate)` 做跳变而非平滑 | 同 | 0 ／ `ML` |
| `sg_vdisp_grid_snap` | in: `posOS(f3)` ／ out: `posOS(f3)` | `pixelSize f`、`space i` | 顶点栅格吸附（像素风的几何侧，`STYLE_IMPL_CARTOON_PIXEL.md` §3.6） | 同 | 0 ／ `ML` |
| `sg_vdisp_shell_extrude` | in: `posOS(f3)`, `normalOS(f3)` ／ out: `posOS(f3)` | `width f`、`widthMode i`（`object / viewScaled`） | 法线外扩描边壳（3D 卡通描边）。与 `SG_VfxLit` 的 `Cull Front` 变体配对 | 仅 3D | 0 ／ `MH`（ML/MM 禁用，见 §10） |

### 3.7 合成族（composite）

| id | 端口 | 参数 | 用途 | 2D-3D | sampleCost / minTier |
|---|---|---|---|---|---|
| `sg_comp_smoothmin` | in: `a(f)`, `b(f)` ／ out: `d(f)` | `k f 0.01~1`、`mode i`（`polynomial / exponential`） | SDF 平滑并集：液态张力、液滴合并（`core / emission 材质 / ground / decal / link`） | 同 | 0 ／ `ML` |
| `sg_comp_dark_core` | in: `baseColor(f4)`, `mask(f)` ／ out: `color(f4)` | `darkness f 0~1`、`rimColor f3`、`rimWidth f` | 暗核乘算：中心把背景乘暗，边缘加色。配合 `SG_VfxSurface` 的 `_BLEND_MULTIPLY` 关键字。这是"吸光"语义唯一合法实现（Unity 局部光不能为负） | 2D 对排序敏感（暗核必须在加色边之下），编译器按 §11 排序表分配 | 0 ／ `ML` |
| `sg_comp_hdr_grade` | in: `field(f)`, `alpha(f)` ／ out: `color(f3)`, `stepId(f)` | `stopCount i 3~4`、`stopThresholds f4`、`stopMultipliers f4`、`softness f` | **强度台阶分级**：REFERENCE_ANALYSIS §2-1「亮度必须分级而非渐变，至少三到四个可辨识台阶」的唯一落点。详见 §7.2 | 同 | 0 ／ `ML` |
| `sg_comp_palette_lut` | in: `t(f)`, `stepId(f)` ／ out: `color(f3)` | `mode i`（`gradient / discrete`）、`stops f4`（5 段位置） | 线性 HDR 色板取色。`discrete` 模式不插值（像素 / 卡通）。可选接一张离线生成的梯度 LUT 纹理（ADR-010 §5 允许的辅助素材），但**默认不接**：5 色解析插值足够且零采样 | 同 | 0（默认）/ 1（接 LUT）／ `ML` |
| `sg_comp_glow_stack` | 见 §5 | 见 §5 | 自带辉光层（ADR-010 §4bis-6） | 见 §5 | 见 §5 |
| `sg_comp_refract` | 见 §6 | 见 §6 | 折射可选层（ADR-010 §4bis-1） | 见 §6 | 见 §6 |
| `sg_comp_soft_depth_fade` | in: `screenPos(f4)`, `eyeDepth(f)` ／ out: `fade(f)` | `distance f`、`power f`、`fallbackMode i` | 与场景几何相交处软化（`body / veil / ground / column / 辉光片`）。依赖 Depth Texture，降级路径见 §5.7 | 仅 3D（2D 正交下无意义，编译期直通） | 1（深度采样）／ `MH` |
| `sg_comp_veil_layer` | in: `n(f)`, `dist(f)` ／ out: `a(f)`, `color(f3)` | `maxAlpha f 0.02~0.2`、`sizeMul f 1.5~6`、`softness f 0.4~1` | **低亮度大范围雾幕**：REFERENCE_ANALYSIS §2-4 的落点。它不是一个新形态，而是一组"大尺寸 + 极低 alpha + 极软阈值 + 排序在主体之下"的强制约束，封装成子图以保证元素预设不会把它调成实体 | 同（2D 需 sorting 偏移） | 0 ／ `ML` |
| `sg_passthrough_f` / `_f2` / `_f3` / `_f4` | 直通 | — | 未使用槽位的零成本占位（保证主图槽位数固定） | 同 | 0 ／ `ML` |

---

## 4. `StyleStage` 插槽契约

### 4.1 位置

`StyleStage` 是**每张主图末端的最后一个子图槽位**，位于全部形态 / 颜色计算之后、主图输出之前。它是风格轴"完全独立于内容"的唯一材质侧叠加点（`STYLE_CATALOG_v1.md` §1.2 叠加点①）。

```
… → sg_comp_hdr_grade → sg_comp_palette_lut → [StyleStage] → 主图输出（Base Color / Alpha / Emission）
```

**约束**：`StyleStage` 之后不得再有任何形态或颜色节点。谓词 MG-3（§12）断言这一点。

### 4.2 接口契约（三个子图必须严格一致）

| 端口 | 方向 | 类型 | 语义 |
|---|---|---|---|
| `inColor` | in | f3（线性 HDR，可 > 1） | 元素与原型算出的颜色 |
| `inAlpha` | in | f | 阈值输出的覆盖度 |
| `inEdgeDist` | in | f | 到形状边的带符号距离（`sg_threshold_*` 的 `edgeDist`）；无形状概念的层传 1 |
| `inNormalWS` | in | f3 | 世界法线；2D / 无法线层传 `(0,0,-1)` |
| `inViewWS` | in | f3 | 世界视线方向；`SG_VfxCanvas` 传 `(0,0,-1)` |
| `inStepId` | in | f | `sg_comp_hdr_grade` 的台阶编号（0~3） |
| `inPixelUv` | in | f2 | `sg_flow_grid_snap` 输出的虚拟像素坐标；未启用时传屏幕 UV |
| `inLayerKind` | in | i（静态） | 层类别：`0=solid`（实体/壳）`1=volume`（体积/雾）`2=line`（线/带）`3=ui`。**风格按类别改变行为**（如体积类默认不描边，`STYLE_CATALOG_v1.md` §2.7） |
| `outColor` | out | f3 | 最终颜色（线性） |
| `outAlpha` | out | f | 最终 alpha |
| `outOutlineColor` | out | f3 | 描边色（供主图在 `Cull Front` 描边壳变体中使用；无描边时等于 `outColor`） |

### 4.3 三个实现

| 子图 | 行为 | 参数来源 |
|---|---|---|
| `sg_style_none` | 恒等直通：`outColor = inColor`，`outAlpha = inAlpha`，`outOutlineColor = inColor`。**零指令**（写实基线） | 无 |
| `sg_style_cartoon` | 色阶量化 + 锐利阈值 + SDF 描边 / 暗边 + 高饱和 + HDR 压台阶 | `style.parameters`（`STYLE_IMPL_CARTOON_PIXEL.md` §2.6 默认值表） |
| `sg_style_pixel` | 栅格量化（已在上游由 `sg_flow_grid_snap` 做坐标吸附，本子图做**颜色与 alpha** 的量化）+ 有限色带 + alpha 二值化 + Bayer 抖动 + 1 格描边 | 同上 §3.7 |

### 4.4 编译期绑定

`style.id` 直接决定挂哪个子图，**不进参数合并链**（`STYLE_CATALOG_v1.md` §1.2）。编译器在生成材质时设置静态关键字 `_STYLE_NONE / _STYLE_CARTOON / _STYLE_PIXEL`，三者互斥且必有其一。谓词 ST-1（§12）断言每张产物材质恰好启用一个。

---

## 5. 辉光层规格（自带光晕）

### 5.0 定位与边界

ADR-010 §4bis-6 裁定：**光晕跟着特效走 = 可自带；光晕改变整个画面 = 归用户**。本节规定的辉光层只影响特效自身占据的屏幕区域，是加法混合的柔光片 / 壳，形态 shader 内程序化计算（无贴图）。全屏 Bloom 不在本族，也不在任何族。

REFERENCE_ANALYSIS §3bis 给出 8 个参数，用户点名要求"可调教"。以下逐个给实现方式。

### 5.1 载体：`Glow_k` 子节点组

任何材质族层可声明 `glow` 参数块。编译器为该层生成 `glowLayerCount` 个子节点：

```
Layers/<layerId>/Glow_0 … Glow_{n-1}
├─ MeshRenderer + MeshFilter（3D）/ SpriteRenderer（2D）
│   网格 = quad（billboard，跟随相机）或与宿主层同形的放大壳（3D 实体宿主）
├─ Material：SG_VfxAdditive + sg_comp_glow_stack
└─ 无 Collider、无脚本
```

- **为什么是多个 renderer 而不是一张 shader 内循环**：`layerCount` 是"体积散射厚度"的来源，每层有独立的半径倍率、强度倍率与 breakup 相位；用真实的多张加法片使它们**天然按深度正确混合**，也使 overdraw 成本对预算模型是诚实可数的（每片 1 层 overdraw，直接进 §2.4 的"透明叠加层上限"）。shader 内循环会把成本藏起来并在同一深度平面互相覆盖，失去厚度感。
- **billboard 还是壳**：`glowShape` 参数二选一。`billboard`（默认）= 一张朝向相机的 quad，尺寸 = 宿主包围球半径 × `glowRadius` × `layerRadiusRatio^k`；`shell` = 复制宿主网格并沿法线外扩，用于宿主是实体几何且需要光晕贴合轮廓的场合（成本高，`minTier = PL`）。2D 恒为 `billboard`。
- **不生成自己的光**：辉光层是渲染，不是 `local_light`。二者的耦合见 §5.8。

### 5.2 `glowRadius` — 光晕半径相对核心的倍数

- **实现**：两处生效。(a) 几何侧：`Glow_k` 的 localScale = `hostRadius * glowRadius * pow(layerRadiusRatio, k)`；(b) 材质侧：`sg_comp_glow_stack` 内部把归一化到片内的距离 `d = length(uv*2-1)` 直接作为衰减输入，因此片的尺寸就是光晕的尺寸，无需第二个半径参数。
- **范围**：`1.0 ~ 6.0`（相对宿主包围球）。默认 1.8。
- **与朦胧感**：大半径 + 低 `layerIntensity` = 朦胧；小半径 + 高强度 = 锐利。这条对应关系由 §5.9 的风格默认值表固化。

### 5.3 `falloffCurve` — 衰减曲线四型的节点实现

`d ∈ [0,1]` 为归一化距离，输出 `w ∈ [0,1]`。四型用**静态关键字分支**（`_FALLOFF_GAUSSIAN / _EXP / _LINEAR / _STEP`）实现，不是运行时 `if`，因此只有被选中的那条进最终指令流。

| 型 | 公式 | 节点实现 | 观感 |
|---|---|---|---|
| `gaussian` | `w = exp(-k * d²)`，`k = -ln(cut)/1` 取 `cut=0.004` 得 `k≈5.5` | `Multiply(d,d) → Multiply(-5.5) → Exponential(Base E)` | 雾感、最朦胧。写实 / 默认 |
| `exp` | `w = exp(-k * d)`，`k≈5.5` | `Multiply(-5.5) → Exponential` | 近核集中、外围拖长尾 |
| `linear` | `w = saturate(1 - d)^p`，`p = falloffPower 0.5~4` | `OneMinus → Saturate → Power` | 可控硬度的中间态 |
| `step` | `w = 1 - floor(d * s) / s`，`s = falloffSteps 2~5`，再乘各阶权重 `stepWeights` | `Multiply(s) → Floor → Divide(s) → OneMinus`，可选 `Sample` 阶权 | **硬光圈**，卡通默认 |

`falloffPower` 与 `falloffSteps` 是同一个 `float4 _FalloffParams` 的分量，避免属性数膨胀。

### 5.4 `layerCount` / `layerRatio` — 层数与层比

- `layerCount i 1~4`：生成的 `Glow_k` 子节点数。**编译期决定，不是运行时**（子节点数是预制体结构）。
- `layerRadiusRatio f 1.2~2.2`：第 k 层半径 = 基半径 × `ratio^k`。
- `layerIntensityRatio f 0.25~0.8`：第 k 层强度 = 基强度 × `ratio^k`（外层更暗）。
- **为什么单层永远显薄**：单层只有一条衰减曲线，屏幕上是一个各向同性的亮度梯度；多层不同半径的加法叠加产生**分段斜率**，近似体积散射的多次散射项，这正是 REFERENCE_ANALYSIS §2-2「硬柔并存」中"柔"的那一半。
- **档位联动**：`layerCount` 受 §2.4 的透明叠加层上限约束，见 §10.3。

### 5.5 `breakupNoise` — 边缘噪声打散（消除"贴纸感"）

**问题**：完美圆形的加法柔光片是廉价特效最典型的特征（"贴纸感"）。

**实现（三处叠加，缺一不可）**：

1. **角向半径调制**：`theta = atan2(p.y, p.x)`，用 `sg_noise_value_1d(theta * angularFreq + seedK)` 得 `r0 ∈ [-1,1]`，把距离改为 `d' = d * (1 + breakupNoise * amplitudeAngular * r0)`。`angularFreq` 默认 3~7（低频，出"不规则轮廓"而不是毛刺）。
2. **二维细节调制**：`sg_noise_fbm_aniso(p * detailFreq + flow, octaves=1)` 乘到最终 `w` 上，权重 `breakupNoise * 0.35`。使内部也有明暗不均。
3. **逐层去相关**：第 k 层的 `seedK = _Seed + k * 17.0`，且 `angularFreq_k = angularFreq * (1 + 0.23k)`。**必须**：否则多层的凹凸对齐，叠出来仍是一个规则形状，等于没打散。

`breakupNoise f 0~1`，0 = 完美圆（几何 / 光元素的洁净读感需要它），1 = 强不规则。默认 0.35。`sampleCost` = 1（第 1 项是 1D，第 2 项是 1 层 fbm，合计计 1）。

### 5.6 `anisotropy` — 沿运动轴拉伸

- **实现**：在计算 `d` 之前，把片内局部坐标沿指定轴做非均匀缩放：
  `p' = float2(dot(p, axis2D) / (1 + anisotropy), dot(p, perp2D))`，然后 `d = length(p')`。
  轴向拉伸 = 除以 `(1+a)` 使该方向的等值线拉长。
- **`axis` 的来源（三选一，由 `anisotropyAxisMode` 决定）**：
  - `velocity`（默认，行进类层）：控制器每帧把宿主的世界速度归一化后写入 `_GlowAxisWS`（`MaterialPropertyBlock`）。速度低于 `axisMinSpeed` 时平滑回退到 `anisotropy = 0`（避免静止时随机拉伸）。
  - `localAxis`：recipe 给定的物体空间轴（`beam_column / link / column` 用）。
  - `screenAxis`：屏幕空间固定角（`fill / frame` 等 UI 类用）。
- **billboard 片下的轴变换**：片是朝向相机的，需把世界轴投影到片的切空间后归一化；若投影长度 < 0.15（轴几乎垂直于屏幕），线性淡出 `anisotropy` 到 0——正对着看的拉伸没有意义且会抖。
- `anisotropy f 0~3`。默认 0（各向同性），行进类层的元素预设给 0.6~1.8。

### 5.7 `softDepthFade` — 与场景几何相交处软化

- **主路径（需要 Depth Texture）**：`sg_comp_soft_depth_fade` 采样 `_CameraDepthTexture`，`fade = saturate((sceneEyeDepth - fragEyeDepth) / distance)`，再 `pow(fade, power)`。
- **依赖**：URP `Depth Texture` 开关（Pipeline Asset 或 Camera 覆写）。与折射的 `Opaque Texture` 是两个独立开关，检测机制同构（§6.2），但**判定是分开的**。
- **降级路径（Depth Texture 关闭时，三级）**：
  1. `geometricSoften`（默认降级）：把 `Glow_k` 的 quad 换成**朝向相机的半球壳片**（`glowShape = domeBillboard`），并在材质里按 `边缘 → 中心` 的法线-视线夹角额外淡出。它不能真的知道场景深度，但把"硬切边"变成"逐渐变薄的边"，在插入地面时的破绽显著小于纯 quad。成本：顶点数 quad 的 8 倍（33 顶点 vs 4），无额外采样。
  2. `shrink`：把最外层 `Glow_{n-1}` 关闭并把整体 `glowRadius` 乘 0.8，减少与几何相交的概率。用于 `ML/MM` 档（那里本就没有 depth texture 预算）。
  3. `off`：`fade = 1`。仅当层被声明为"不会与几何相交"（`fill / frame / orbit` 等）时使用。
- **降级选择是编译期的**：编译器在 `PLATFORM_PROFILE`（§6.3）里读到 depth texture 不可用时，把 `sg_comp_soft_depth_fade` 替换为直通并按上表设置 `glowShape`，在编译报告中登记。**运行时不做二次判定**——深度纹理开关不像 Opaque Texture 那样有"两条路都要能跑"的强诉求，且几何形状差异无法在运行时切换。
- **2D**：正交相机 + Sorting Layer，恒为 `off`（编译期直通）。
- **`SG_VfxCanvas`**：恒为 `off`。

### 5.8 `flickerCoupling` — 与局部光节拍同步

- **实现**：`glowIntensity = baseIntensity * lerp(1, _BeatValue, flickerCoupling)`。`_BeatValue ∈ [0,1]` 由局部光节拍器组件（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §6）**每帧写入一次**，通过 `MaterialPropertyBlock` 广播到该层及其全部 `Glow_k` 子节点。
- **为什么不各自算 time**：两处独立按 `_LocalTime` 算同一个闪烁函数，在时间量化（像素风 `frameRate`）、`speed` 参数变化、池化复位这三种情况下都会错相。让节拍器成为唯一的相位所有者，光与光晕天然同呼吸。
- **`光:烘` 档（ML）没有 Light 组件**：节拍器组件仍然存在并计算 `_BeatValue`（它只是不再驱动 `Light.intensity`，改为驱动材质发光倍率）。因此 `flickerCoupling` 在全部六档语义一致。
- `flickerCoupling f 0~1`。默认 0.6。

### 5.9 `innerColor` / `outerColor` — 内外色分离

- **实现**：`color = lerp(outerColor, innerColor, pow(w, colorMixPower))`，`w` 为 §5.3 的衰减权重。`colorMixPower f 0.5~3` 控制内色的占比范围。
- **默认取色**：`innerColor = _PaletteC`（`hot`），`outerColor = _PaletteA`（`primary`）。这自动实现了 REFERENCE_ANALYSIS §1-A 观察到的"内白外暖"；内白外冷则由元素色板把 `primary` 设为冷色达成——**不需要额外机制**。
- 两个色都是线性 HDR，可 > 1。

### 5.10 参数面汇总（`glow` 参数块）

| 参数 | 类型 | 范围 | 默认 | 生效位置 |
|---|---|---|---|---|
| `enabled` | bool | — | 按层角色（见 §5.11） | 编译期决定是否生成子节点 |
| `glowRadius` | float | 1.0~6.0 | 1.8 | 几何 + 材质 |
| `falloffCurve` | enum | `gaussian/exp/linear/step` | `gaussian` | 静态关键字 |
| `falloffPower` | float | 0.5~4 | 1.5 | `_FalloffParams.x` |
| `falloffSteps` | int | 2~5 | 3 | `_FalloffParams.y` |
| `layerCount` | int | 1~4 | 2 | 子节点数（编译期） |
| `layerRadiusRatio` | float | 1.2~2.2 | 1.55 | 每层 scale |
| `layerIntensityRatio` | float | 0.25~0.8 | 0.5 | 每层强度 |
| `breakupNoise` | float | 0~1 | 0.35 | 材质 |
| `breakupAngularFreq` | float | 2~12 | 5 | 材质 |
| `anisotropy` | float | 0~3 | 0 | 材质 |
| `anisotropyAxisMode` | enum | `velocity/localAxis/screenAxis` | `velocity` | 绑定来源 |
| `innerColor` | color(HDR) | — | `_PaletteC` | 材质 |
| `outerColor` | color(HDR) | — | `_PaletteA` | 材质 |
| `colorMixPower` | float | 0.5~3 | 1.4 | 材质 |
| `softDepthFade` | float | 0~2（米） | 0.5 | 材质（含降级） |
| `flickerCoupling` | float | 0~1 | 0.6 | 材质 |
| `glowShape` | enum | `billboard/domeBillboard/shell` | `billboard` | 编译期几何 |

全部 18 项在 Inspector（参数块组件）、Recipe（`layers[].parameters.glow.*`）、运行时脚本（绑定表 `material.glow.*`）三处均可调，符合 ADR-010 §4bis-6"必须参数化可调教"。

### 5.11 默认开启的层角色

| 角色 | 默认 `glow.enabled` | 理由 |
|---|---|---|
| `core / flash / edge / beam_column / link / column / shock` | true | 有发光语义的主体，光晕溢出是 REFERENCE_ANALYSIS §2-6 的必要条件 |
| `body / veil / ground / surface / fill / frame` | false | 这些本身就是大面积低对比层，再叠光晕只增 overdraw 不增读感；需要时由 recipe 显式开 |
| `trail / orbit / decal / debris / mesh_shell` | false | 由元素预设按需开（如发光元素的 `trail`） |
| `emission`（粒子层） | 不适用 | 粒子的光晕由粒子材质自身的软边承担，不生成 `Glow_k` 子节点（见 `TECH_FAMILY_SPEC_PARTICLES.md` §2.5） |

---

## 6. 折射可选层（optional refraction layer）

### 6.0 裁定回顾

ADR-010 §4bis-1：采用**可选层 + 自动降级**。运行时检测 URP `Opaque Texture`，开启则走 Scene Color 真折射，未开启自动退回法线扰动，**两条路均不报错**。T2a 曾建议 v1 不做（`T2A_REPORT.md` §6-1），已被用户裁定推翻。

### 6.1 双路实现

`sg_comp_refract` 子图，两条路由静态关键字 `_REFRACT_SCENECOLOR` 切换：

| 路 | 关键字 | 实现 | 视觉能力 | 成本 |
|---|---|---|---|---|
| **真折射** | `_REFRACT_SCENECOLOR` 开 | `screenUv' = screenUv + refractOffset`，`refractOffset = (normalTS.xy * strength * invDistance)`；`Scene Color` 节点采样 `_CameraOpaqueTexture` 得背景色，与本层颜色按 `refractBlend` 混合 | 背景真实位移，可见"透过看到的东西被扭曲" | 1 次纹理采样 |
| **法线扰动降级** | 关 | 用同一个 `normalTS` 计算高光偏移：`spec = pow(saturate(dot(reflect(-view, perturbedNormal), lightDirApprox)), specPower)`，并按 `|normalTS.xy|` 调制 `edge` 的亮度，制造"表面有起伏"的读感 | 无背景位移，但保留"这个表面不是平的"的信息 | 0 次采样 |

**关键设计：两条路共享同一个 `normalTS` 上游**（由 `sg_noise_fbm_aniso.grad` 或 `sg_vdisp_gerstner.normalOS` 或 `sg_sdf_*.grad` 产生）。因此切换不改变形态、不改变参数面，只改变"这个法线被用来干什么"。这是"两条路均不报错、参数面一致"的实现基础。

### 6.2 运行时检测机制

**检测什么**：当前激活的 URP Pipeline Asset 的 `supportsCameraOpaqueTexture`。

**在哪检测**：预制体根上的运行时组件 `VfxUrpCapabilityProbe`（`COMPILER_BOUNDARY_V2.md` §5.6 的识别组件闭集成员）。

**检测逻辑（伪码，实现留 T3）**：

```
Awake():
  asset = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline
  urp   = asset as UniversalRenderPipelineAsset
  ok    = urp != null && urp.supportsCameraOpaqueTexture
  // Camera 级覆写（URP 的 UniversalAdditionalCameraData.requiresColorOption）
  // 无法在 Awake 可靠得知目标相机，因此只按 Pipeline Asset 判定；
  // 若用户在相机上关掉了 Opaque Texture，真折射路会采到上一帧或黑色——
  // 这是用户显式关闭的后果，属于"两条路都不报错"的合法结果，不做二次探测。
  ApplyRefractionRoute(ok)
```

**切换机制（三选一，选定 B）**：

| 方案 | 描述 | 裁定 |
|---|---|---|
| A：双材质切换 | 编译期产出两份材质，运行时换 `sharedMaterial` | **否**。产物目录材质数翻倍，`VfxOutputAuditor` 的 `MaxLocalMaterials` 预算被吃掉一半，且两份材质的参数需要同步写两次 |
| **B：本地关键字（采用）** | 一份材质，shader 内 `multi_compile_local _ _REFRACT_SCENECOLOR`，运行时对**克隆材质**调 `EnableKeyword/DisableKeyword` | **是**。材质数不变、参数面唯一；用 `multi_compile`（不是 `shader_feature`）保证两个变体在 Player 构建中都不被剥离；作用于编译器克隆的 per-prefab 材质，同一预制体所有实例共享同一 URP 配置，语义正确 |
| C：运行时动态分支 | shader 内 `if (_HasOpaque)` | **否**。Scene Color 节点在关闭 Opaque Texture 时采样的是未定义资源，动态分支不能免除采样声明；且两路都进指令流，白付成本 |

**参数面对齐**：`refractStrength / refractBlend / specPower / specGain` 四个参数在两条路都有意义（降级路用前二者调制法线扰动的可见度），Recipe 无需知道走了哪条路。编译报告与运行时 `VfxUrpCapabilityProbe.ActiveRoute` 属性记录实际路由，供画廊截帧对比时标注（`GALLERY_SPEC.md` §6.4）。

### 6.3 编译期的 `PLATFORM_PROFILE`

编译器不能假设编译机的 URP 配置等于用户运行时的配置。因此：

- 编译期**永远**产出双关键字变体的材质（不做剪裁），`_REFRACT_SCENECOLOR` 的初值按编译机检测结果设置（只是一个初值，Awake 会覆盖）。
- 编译报告声明该 recipe 的产物有"两种 URP 配置下的两种表现"，并列出受影响的层（`COMPILER_BOUNDARY_V2.md` §9）。
- Depth Texture 的处理**不同**（§5.7）：它影响几何形状，无法运行时切换，因此按编译机的 `PLATFORM_PROFILE` 编译期定死并登记。

### 6.4 Queue Offset 与自反射规避

- **渲染顺序**：URP 的 `_CameraOpaqueTexture` 在不透明 pass 之后、透明 pass 之前拷贝。因此**任何透明层都不会采样到任何透明层**（包括它自己与同预制体的其他层）——自反射天然不成立，无需额外规避。
- **但需要控制透明层之间的绘制顺序**，使折射层在辉光之下（否则辉光被折射层的背景色冲淡）。渲染队列偏移表（相对 `Transparent` = 3000）：

| 层类别 | Queue Offset | 说明 |
|---|---|---|
| `veil`（低亮雾幕） | −20 | 最先画，垫在所有主体之下 |
| `body / decal / ground / surface` | −10 | |
| 折射层（`_REFRACT_SCENECOLOR`） | −5 | 必须早于加法层 |
| `core / mesh_shell / trail / link / beam_column / column` | 0 | 主体 |
| `edge / shock` | +5 | |
| `Glow_k`（加法辉光） | +10 + k | 外层在内层之后画（k 大者在后），保证加法叠加顺序稳定 |
| `flash` | +20 | 最后画的高亮 |

2D 下 Queue 不起作用，同一张表转成 `sortingOrder` 偏移（§11）。

---

## 7. HDR / 线性空间纪律

### 7.1 色板在线性空间的处理

1. **元素色板的 5 个值是线性 RGB**（`ELEMENT_CATALOG_v1.md` §1.1），`hot` 分量可 > 1（如 `(6.0, 6.0, 8.0)`）。它们以 `Color` 属性（`[HDR]` 标记）进材质，Unity 在 Linear 色彩空间工程下不做转换。
2. **项目必须是 Linear 色彩空间**。谓词 PR-1（§12）断言 `PlayerSettings.colorSpace == ColorSpace.Linear`。Gamma 工程下全部亮度分级的比例关系失效，这不是"看起来差一点"，是分级机制不成立。
3. **不得在 shader 内做 sRGB↔Linear 转换**。任何 `Colorspace Conversion` 节点在本族的材质中都是错误信号；谓词 SG-6 断言子图库内零出现。
4. **梯度取值一律在线性空间插值**（`sg_comp_palette_lut`）。若元素预设希望"看起来均匀"的渐变，应在色板值上体现，不在 shader 里做感知空间插值。
5. **`_Intensity` 是线性倍率**，作用在 `sg_comp_hdr_grade` 之后、`StyleStage` 之前。

### 7.2 强度分级：三到四个可辨识台阶

REFERENCE_ANALYSIS §2-1 的准则是本族最重要的一条质量约束，它由 `sg_comp_hdr_grade` 单点实现。

**为什么不是渐变**：连续渐变在 HDR + Bloom 下会被压成一团中间调，读不出"能量层次"；离散台阶在任何曝光下都保持可辨识的边界。

**实现**：

```
输入 field ∈ [0,1]（形态强度场，通常是噪声与阈值的组合）
stopCount ∈ {3, 4}
stopThresholds = (t0, t1, t2, t3)   // 升序，t0 = 0
stopMultipliers = (m0, m1, m2, m3)  // 各台阶的强度倍率

stepId = 0
for k in 1..stopCount-1:  stepId += step(t_k, field)   // 无分支，累加 step()
mul    = stopMultipliers[stepId]                        // 用 4 分量点乘 one-hot 取值，无索引
// 台阶内保留少量斜率，避免死板：
local  = saturate((field - t_stepId) / max(t_{stepId+1} - t_stepId, 1e-4))
mul   *= lerp(1.0, 1.0 + softness, local)               // softness ∈ [0, 0.35]
color  = palette(stepId) * mul * _Intensity
```

**默认台阶表（元素预设可覆写，但必须满足下面的谓词）**：

| stepId | 语义 | 默认阈值 | 默认倍率 | 对应色板槽 |
|---|---|---|---|---|
| 0 | 残留 / 暗部 | 0.00 | 0.06 | `residue` |
| 1 | 主色暗侧 | 0.35 | 0.30 | `cool` |
| 2 | 主色 | 0.62 | 1.00 | `primary` / `secondary` |
| 3 | 白热核 | 0.86 | 4.00 | `hot` |

倍率比 `0.06 : 0.30 : 1.00 : 4.00` ≈ 每级 3.3~5 倍，在任何曝光下都跨越至少一个可辨识的亮度台阶。

**构造性谓词（HG-1，机器可查，EditMode，不需渲染）**：对任意产物材质，读 `_StopMultipliers` 与色板，计算每个台阶的合成亮度 `L_k = luminance(palette(k)) * m_k`；断言 `stopCount ≥ 3` 且相邻台阶满足 `L_{k+1} / L_k ≥ 2.0`。低于 2.0 即判定为"渐变化"，FAIL。这把一条美学准则变成了一条数值断言。

### 7.3 `softness` 与"台阶不死板"

`softness = 0` 是纯色块（卡通 / 像素风的诉求），`softness = 0.35` 让每个台阶内部有约 35% 的亮度斜率（写实基线的诉求）。它**不改变台阶数**，因此不破坏 HG-1。

### 7.4 与用户侧 Bloom 的关系

REFERENCE_ANALYSIS §2-6：自包含辉光层承担七八成，用户侧 Bloom 补足。本族的义务是：

- `hot` 台阶的输出**必须** > 1.0（否则用户开 Bloom 也不会有溢出），谓词 HG-2 断言 `luminance(palette.hot) * m_3 * 默认 _Intensity ≥ 1.5`。
- 同时，在**关闭** Bloom 时（画廊的裸质量模式，`GALLERY_SPEC.md` §5）观感必须成立——这由 §5 的自带辉光层保证。两者的分工是"自带辉光给形，Bloom 给溢出"。

---

## 8. 材质族变体清单（`technique.variant`）

变体 = 主图 + 关键字 + 槽位子图 + 参数面。以下 25 个变体覆盖 18 个层角色的全部材质需求（含 F 类 Overlay Canvas 的伪粒子变体）。表中"槽位"按 §2.3 的流水线顺序写（`Space/Flow → Shape → Threshold → Edge → Composite`）。

| # | variant id | 主图 | 关键槽位组合 | 服务层角色 | 维度 | 参数面（除统一属性外） | sampleCost | minTier |
|---|---|---|---|---|---|---|---|---|
| 1 | `mat_volume_fbm` | `SG_VfxSurface` | `flow_along_axis` → `noise_fbm_aniso` → `threshold_soft` → `edge_sdf_rim` → `hdr_grade` | `core / body / trail / veil` | 2d,3d | `octaves, aniso, flowDir, speedRatio, thresholdWidth, tearBias` | 1~3 | ML |
| 2 | `mat_volume_march` | `SG_VfxSurface` | `sdf_primitive_3d` + `edge_thickness` → `noise_fbm_aniso` → `threshold_soft` | `body / core`（3D 体积感） | 3d | 同上 + `thickness, densityCurve` | 3~4 | PL |
| 3 | `mat_crystal_cell` | `SG_VfxSurface` / `SG_VfxLit` | `noise_voronoi_cell` → `threshold_hard` → `edge_fresnel` | `core / mesh_shell / edge / debris` | 2d,3d | `density, jitter, metric, fresnelPower, innerScatter` | 2~3 | MM |
| 4 | `mat_jagged_band` | `SG_VfxSurface` | `flow_along_axis` → `noise_jagged_1d` → `threshold_dual` | `link / beam_column / trail / edge` | 2d,3d | `segments, amplitude, rephaseRate, branchDepth, coreWidth, haloWidth` | 1 | ML |
| 5 | `mat_shape_sdf` | `SG_VfxSurface` | `sdf_primitive_2d` → `threshold_soft/hard` → `edge_outline_sdf` | `flash / shock / ground / orbit / core`（quad 类） | 2d,3d | `shape, param, rotation, outlineWidth` | 1 | ML |
| 6 | `mat_ring_polar` | `SG_VfxSurface` | `flow_polar` → `sdf_primitive_2d(ring)` → `threshold_soft` | `shock / ground / edge / frame` | 2d,3d | `innerRadius, outerRadius, angularSpeed, radialSpeed, ringCount` | 1 | ML |
| 7 | `mat_radial_spike` | `SG_VfxAdditive` | `sdf_radial_burst` → `threshold_dual` → `hdr_grade` | `flash / shock / edge / ground` | 2d,3d | `count, lengthRange, widthRange, angleJitter, taper` | 1 | ML |
| 8 | `mat_rune_ring` | `SG_VfxSurface` | `flow_polar` → `sdf_rune_ring` → `threshold_hard` → `edge_outline_sdf` | `ground / mesh_shell / orbit / frame / core` | 2d,3d | `ringCount, ringRadii, tickCount, glyphCount, spinRate` | 2 | MM |
| 9 | `mat_sacred_pattern` | `SG_VfxSurface` | `flow_polar` → `sdf_sacred_pattern` → `threshold_soft` | `ground / flash / frame / veil` | 2d,3d | `symmetry, layerCount, motif, radiusRatio` | 1 | ML |
| 10 | `mat_grid_cells` | `SG_VfxSurface` | `sdf_grid` → `threshold_grow(cellular)` → `edge_outline_sdf` | `mesh_shell / body / fill / surface / debris` | 2d,3d | `gridType, cellSize, lineWidth, growMode, randomDelay` | 1~3 | ML(2d)/MH(3d) |
| 11 | `mat_scanline_sweep` | `SG_VfxSurface` / `SG_VfxCanvas` | `sdf_scanline` → `threshold_hard` | `fill / frame / body / veil / surface` | 2d,3d | `lineCount, dutyCycle, sweepSpeed, sweepWidth, stepRate` | 0 | ML |
| 12 | `mat_crack_grow` | `SG_VfxSurface` | `sdf_crack_branch` → `threshold_grow(directional)` → `edge_outline_sdf` | `decal / mesh_shell.crack / ground / surface` | 2d,3d | `seedCount, branchDepth, angleSpread, width, growthProgress` | 3 | MM |
| 13 | `mat_liquid_blob` | `SG_VfxSurface` | `sdf_primitive_2d/3d` ×N → `comp_smoothmin` → `threshold_soft` → `edge_sdf_rim` + 折射槽 | `core / emission 材质 / ground / link / decal` | 2d,3d | `blobCount, blobRadius, smoothK, rimGain, refractStrength` | 2~4 | MM |
| 14 | `mat_bubble_surface` | `SG_VfxSurface` | `noise_bubble_field` → `threshold_soft` + `vdisp_bulge_pulse` | `core / body / surface / decal` | 2d,3d | `density, riseSpeed, popThreshold, sizeRange, bulgeAmplitude` | 2 | MM |
| 15 | `mat_dark_core` | `SG_VfxSurface`(`_BLEND_MULTIPLY`) | `noise_fbm_aniso` → `threshold_soft` → `comp_dark_core` | `core / body / decal / veil` | 2d,3d | `darkness, rimColor, rimWidth, inflow` | 1~2 | ML |
| 16 | `mat_veil_soft` | `SG_VfxSurface` | `flow_uv_scroll` → `noise_fbm_aniso(octaves=1)` → `threshold_soft` → `comp_veil_layer` | `veil / body / ground` | 2d,3d | `maxAlpha, sizeMul, softness, driftSpeed` | 1 | ML |
| 17 | `mat_surface_wave` | `SG_VfxSurface` | `vdisp_gerstner` + `noise_fbm_aniso` → `edge_fresnel` + 折射槽 | `surface / veil` | 2d,3d | `waveCount, amplitude, wavelength, steepness, refractStrength` | 2~4 | MM |
| 18 | `mat_shell_fresnel` | `SG_VfxLit` / `SG_VfxSurface` | `sdf_grid` 或 `noise_voronoi_cell`（可选）→ `edge_fresnel` → `threshold_soft` | `mesh_shell / core / column / debris` | 3d（2d 用 `mat_shape_sdf`） | `fresnelPower, innerOpacity, facetTint, rippleCount` | 1~2 | ML |
| 19 | `mat_shell_ripple` | `SG_VfxSurface` | `edge_fresnel` + 多点 `sdf_primitive_3d(sphere)` → `threshold_soft` | `flash.hit / mesh_shell / edge` | 2d,3d | `maxSimultaneousHits, rippleDuration, rippleWidth, rippleSpeed` | 1 + hits | MM |
| 20 | `mat_dissolve_edge` | `SG_VfxSurface` / `SG_VfxLit` | `noise_fbm_aniso` 或 `noise_voronoi_cell` → `threshold_grow` → `edge_outline_sdf`（灼边） | `mesh_shell.dissolve / core / body / cloth / debris` | 2d,3d | `mode(burn/shatter/melt/phase), edgeWidth, edgeGain, direction, progress` | 1~3 | ML |
| 21 | `mat_leaf_petal` | `SG_VfxSurface` | `sdf_leaf_petal` → `threshold_hard` → `edge_outline_sdf` | `emission 材质 / orbit / body` | 2d,3d | `width, tipSharpness, veinCount, curvature` | 1 | ML |
| 22 | `mat_glow_stack` | `SG_VfxAdditive` | `comp_glow_stack`（§5） | `Glow_k` 子节点专用 | 2d,3d | §5.10 全部 18 项 | 1 | ML |
| 23 | `mat_ui_fill` | `SG_VfxCanvas` | `sdf_primitive_2d` / `sdf_scanline` / `flow_polar` → `threshold_soft` | `fill` | 2d（UGUI） | `shape, rippleCenter, rippleRadius, sweepAngle, maskToAlpha` | 1 | ML |
| 24 | `mat_ui_frame` | `SG_VfxCanvas` | `sdf_primitive_2d(roundedRect)` → `edge_outline_sdf` → `sdf_scanline`（沿边 U 流动） | `frame` | 2d（UGUI） | `cornerRadius, borderWidth, flowSpeed, segmentCount` | 1 | ML |
| 25 | `mat_ui_pseudo_particles` | `SG_VfxCanvas` | 定长展开的多实例 `sdf_primitive_2d` → `threshold_soft` → `palette_lut`（加法累积） | `emission.*`（Overlay Canvas 下的伪粒子，`TECH_FAMILY_SPEC_PARTICLES.md` §8） | 2d（UGUI） | `instanceCount, motionMode, rateHz, emitPoint, emitRadius, speed, accel, baseSize, sizeCurve, rotSpread, shape` | `instanceCount/8`（1~8） | ML |

**变体命名规则**：`mat_<形态族>_<性格>`，全小写 `^[a-z][a-z0-9_]*$`（与 `recipe-v2.schema.draft.json` 的 `technique.variant` 正则一致）。**禁止**在变体名中出现元素名或原型名——变体是形态能力，元素与原型通过参数注入。

---

## 9. 元素 → 子图组合速查表

本表是 `ELEMENT_CATALOG_v1.md` §3 各元素"材质族预设"的可执行翻译，供 T2c/T3 直接照做。表中只写与默认值不同的关键选择。

| 元素 | 主形态子图 | aniso / 流向 | 阈值 | 边缘 | 顶点位移 | 台阶倍率覆写 | 典型变体 |
|---|---|---|---|---|---|---|---|
| `fire` | `noise_fbm_aniso` ×2 层（速度比 1:2.3） | `aniso=(1, 1.5)`，`+Y` | `soft` 宽 0.15 + 顶部撕裂偏置 | `sdf_rim` 暗红 | `axial_stretch(tongue)` | 默认 | 1, 15(残迹), 20 |
| `ice` | `noise_voronoi_cell(metric=切比雪夫)` + 低频 fbm | 近静止（`speed×0.05`） | `hard` 宽 0.02 + `grow(cellular)` | `fresnel` 硬高光 | `normal_noise`（尖刺） | `m3` 降到 2.0 | 3, 12, 18 |
| `lightning` | `noise_jagged_1d`（`rephaseRate` 10~30） | 不流动，跳变 | `dual`（核 0.005 / 晕 0.3） | 无渐变，硬切 | `tension_jitter(quantize)` | `m3` 提到 8.0 | 4, 7 |
| `water` | `noise_fbm_aniso` 双层流动 + `comp_smoothmin` | 沿重力 / 流向 | `soft` + smoothmin | `sdf_rim` 高光线 + **折射槽启用** | `gerstner` / `axial_stretch` | `m3` 降到 1.2（高光而非发光） | 13, 17 |
| `wind` | `noise_stripe_polar` | 切向 + 轴向上升 | `soft` 宽 0.4，alpha 上限 0.35 | 拉丝淡出 | `sway_bend` | 全体倍率 ×0.5 | 16, 1 |
| `earth` | `noise_fbm_aniso`（高频）+ 层理条纹 | 无 | `hard` | `dark_rim`（粗糙，无发光） | `normal_noise` 大幅 | `m3 = m2`（不发光） | 3, 15, 20 |
| `poison` | `noise_bubble_field` + 低频 fbm | 缓慢向下 / 径向蔓延 | `soft` + 孔洞 | `sdf_rim` 粘稠高光 | `bulge_pulse` | `m3` 降到 1.6 | 14, 12 |
| `light` | 无噪声或权重 0.1~0.2 + `sdf_sacred_pattern` | `+Y` / 径向放射 | `soft` 宽 0.3 | 均匀渐变 | 无 | `m3` 提到 5.0 | 9, 7, 1 |
| `shadow` | `noise_fbm_aniso`（径向拉伸 + 角向） + `comp_dark_core` | 向心 / `-Y` | 撕裂 `soft` | 仅边缘发光（`sdf_rim` 紫） | `sway_bend`（触手） | `m0` 提为主台阶，`m2/m3` 仅边缘 | 15, 1 |
| `arcane` | `sdf_rune_ring` + `sdf_sacred_pattern`，噪声权重低 | 旋转 + 相位脉冲 | `dual`（细线硬 + 晕软） | 双阈值 | 几何呼吸缩放 | 默认 | 8, 9, 7 |
| `tech` | `sdf_grid` + `sdf_scanline` | 阶跃（`flow_time_quantize`） | `hard` | `edge_outline_sdf` 细描边发光 | `vdisp_grid_snap` | `m3` 提到 4.0，双色（`primary`/`secondary`） | 10, 11 |
| `blood` | 低频 fbm（溅斑边）+ 径向拉伸细丝 | 沿重力（V 向阈值推进） | `soft` + smoothmin | `sdf_rim` 微高光（随 `dryness` 转哑） | 滴拉伸 | 全体 ×0.6，`m3` = 1.0 | 13, 12 |
| `nature` | 低频 fbm + `sdf_leaf_petal.veinMask` | 生长方向（U 向推进） | `soft` + `grow(directional)` | 叶脉线 + 半透 | `sway_bend` | `m3` 降到 2.0 | 21, 12, 20 |
| `none.dust` | `noise_fbm_aniso(octaves=1)` 大团 | 缓慢上升 / 贴地扩散 | `soft` 宽 0.35，alpha ≤ 0.3 | 无 | 无 | `m3 = m2`（不发光） | 16 |
| `none.rubble` | 源材质或中性岩纹 | 无 | `hard` | `dark_rim` | 无 | 不发光 | 3, 18 |
| `none.cloth` | 低频布纹 + 边缘磨损阈值 | 无 | `soft` | `dark_rim` | `sway_bend` / `gerstner` | 不发光 | 1, 20 |
| `none.liquid` | 同 `water` 但色板无色 | 同 water | 同 water | 高光 + 折射槽 | 同 water | `m3 = 1.2` | 13, 17 |
| `none.spark_metal` | `sdf_primitive_2d(capsule)` 极短亮线 | 沿速度 | `hard` | 无 | 无 | `m3` 提到 3.0，寿命末硬切 | 5 |

---

## 10. 六档降级机制规格

### 10.1 降级的三个旋钮

编译器对材质族层只有三个降级动作，按顺序施加：

1. **子图替换**：把高成本子图换成同端口的低成本子图（表 §10.2）。
2. **参数截断**：`octaves`、`layerCount`、`steps`、`hits` 等整数参数按档上限截断。
3. **层关闭**：降级表写 `关` 的层，`enabled = false`（但接口绑定保留为空操作，`RECIPE_V2_SCHEMA_DRAFT.md` §7-5）。

### 10.2 子图替换表

| 原子图 | ML | MM | MH / PL | PM / PH |
|---|---|---|---|---|
| `sg_noise_fbm_aniso` | `octaves=1`，`speed` 保留（`材:静` 时接 `sg_passthrough_f` 并用固定阈值） | `octaves=1` | `octaves=2` | `octaves=3` |
| `sg_noise_voronoi_cell`（3D） | 替换为 2D 版 + 三平面关 | 2D 版 | 3D 版 | 3D 版 |
| `sg_noise_curl` | 替换为 `sg_noise_fbm_aniso(octaves=1)` 的梯度近似 | 同 ML | 2D curl | 3D curl |
| `sg_sdf_grid`（3D 三平面） | 单平面投影（主轴） | 单平面 | 三平面 | 三平面 |
| `sg_edge_thickness` | 直通（`thick = 1 - edgeDist`） | 同 ML | 解析厚度 | 解析厚度 |
| `sg_flow_parallax` | 直通 | 直通 | 直通 | `steps=2`（PM）/ `steps=4`（PH） |
| `sg_comp_soft_depth_fade` | 直通 + `glowShape=shrink` | 同 ML | 深度采样 | 深度采样 |
| `sg_vdisp_*` | 全部直通（`材:静` 或顶点数不足） | 仅 `axial_stretch` / `sway_bend` / `tension_jitter`（零采样类） | 加 `normal_noise`（不重算法线） | 全开（`normal_noise` 重算法线） |
| `sg_vdisp_shell_extrude` | 禁用（改 `sg_edge_dark_rim`） | 禁用（改 `sg_edge_dark_rim`） | 启用 | 启用 |
| `sg_comp_refract` | 强制降级路（不采样 Scene Color） | 强制降级路 | 按运行时检测 | 按运行时检测 |

### 10.3 辉光层的档位截断

| 档 | `layerCount` 上限 | `glowShape` 允许 | 说明 |
|---|---|---|---|
| ML | 1 | `billboard` | 单层；`breakupNoise` 保留（零额外成本的角向项，去掉贴纸感是低端档最需要的） |
| MM | 2 | `billboard` | |
| MH | 2 | `billboard` / `domeBillboard` | |
| PL | 2 | `billboard` / `domeBillboard` | |
| PM | 3 | 全部 | `shell` 需宿主顶点 ≤ 2k |
| PH | 4 | 全部 | |

**注意**：`layerCount` 同时受 §2.4 的"透明叠加层上限"总预算约束——一个预制体全部层的 `Glow_k` 数量之和计入 overdraw，见 `COMPILER_BOUNDARY_V2.md` §4.3。

### 10.4 `材:静` 的确切语义

降级表记法 `材:静` 表示"材质无噪声动画（仅渐变 / 阈值）"。编译期动作：

- 全部 `sg_noise_*` 的 `time` 输入接常数 0（保留噪声形态，去掉动画）；
- 全部 `sg_flow_*` 的 `speed` 置 0；
- 保留 `_Progress` 驱动的阈值推进（这是节拍，不是噪声动画，**不得**一并去掉，否则层失去时间行为并触犯谓词 MV-1）。

---

## 11. 2D 排序规格

2D 下 Queue 不生效，`SG_VfxSurface` 编译为 `SpriteRenderer` 或带 `SortingGroup` 的 `MeshRenderer`。编译器按下表分配 `sortingOrder` 偏移（基准由 recipe 的 `layers[].sorting.order` 或原型层序给出）：

| 层类别 | order 偏移 | 备注 |
|---|---|---|
| `veil` | −40 | |
| `body / ground / decal / surface` | −20 | |
| `Glow_k`（宿主之下的内层，k < 宿主基准时） | −10 | 2D 辉光默认全在宿主**之下**（见下） |
| 折射层 | −5 | 2D 下折射恒走降级路（正交相机 + 2D Renderer 无 Opaque Texture 语义） |
| `core / mesh_shell / trail / link / beam_column / column / debris` | 0 | |
| `edge / shock` | +10 | |
| 描边（卡通 2D） | 宿主 −1 | `STYLE_CATALOG_v1.md` §2.6 |
| `flash` | +30 | |

**2D 辉光的排序裁定**：3D 中辉光在主体之后画（加法叠在主体上）；2D 中若同样置于主体之上，会把主体的暗部台阶冲淡，破坏 §7.2 的分级读感。因此 **2D 默认把 `Glow_k` 全部排在宿主之下**（`−10`），加法层先画、主体后画，主体的暗部保持暗。需要"包住主体"的读感时由 recipe 显式给正偏移。

**乘算层（`comp_dark_core`）的 2D 约束**：暗核必须在其加色边之下且在被吞噬对象之上，编译器强制 `暗核 order = 加色边 order − 1`，违反即 `E2xx`。

---

## 12. 构造性谓词（EditMode 机器检查）

继承 ADR-009 的方法论：**对资产序列化状态或声明清单的确定性断言**，EditMode 下可判定，不进 PlayMode、不渲染。谓词编号稳定，失败信息必须引用编号。

### 12.0 可断言性设计：子图清单（subgraph manifest）

Shader Graph 子图（`.shadersubgraph`）的图结构没有稳定的公开 API，直接解析会随 Unity 版本脆断。因此每个子图**必须**配一份同名的 sidecar 清单：

```
Assets/VFX/Shared/Subgraphs/<id>.shadersubgraph
Assets/VFX/Shared/Subgraphs/<id>.subgraph.json
   { "id", "guid", "ports": [{name, direction, type}], "parameters": [{name, type, min, max, default}],
     "sampleCost", "minTier", "dimensions": ["2d","3d"], "requiresVertexStage": bool,
     "requiresDepthTexture": bool, "requiresOpaqueTexture": bool }
```

谓词断言的是**清单 ↔ 资产 ↔ 产物材质**三者的一致性。这与 ADR-009 "对 manifest 字段 + prefab 组件状态断言"同构。

### 12.1 子图库层谓词（SG-*）

| 编号 | 谓词 |
|---|---|
| SG-1 | `Assets/VFX/Shared/Subgraphs/` 下每个 `.shadersubgraph` 必有同名 `.subgraph.json`；反之每份清单的 `guid` 必须解析到存在的 `.shadersubgraph`。**无清单的子图不得存在**（封死绕过质检的旁路，与 ADR-009 §3-3 同构） |
| SG-2 | 清单 `id` 必须匹配 `^sg_[a-z][a-z0-9_]*$` 且在库内唯一 |
| SG-3 | 清单 `sampleCost` ≥ 0 且 `minTier ∈ {ML,MM,MH,PL,PM,PH}`；`dimensions` 非空 |
| SG-4 | `requiresVertexStage = true` 的子图，其 id 必须以 `sg_vdisp_` 开头（顶点位移族的封闭性） |
| SG-5 | 子图库内**零个**子图声明纹理采样端口，`sg_comp_palette_lut` 除外（唯一允许的可选 LUT 输入）。断言方式：清单 `ports` 中 `type == "Texture2D"` 的端口只允许出现在 `sg_comp_palette_lut` |
| SG-6 | 子图库内零个子图声明 `colorspaceConversion` 能力标记（§7.1-3）。断言方式：清单新增布尔字段 `usesColorspaceConversion`，全库必须为 false |
| SG-7 | 每个 `StyleStage` 实现（`sg_style_none/cartoon/pixel`）的 `ports` 必须与 §4.2 的 9 项契约**逐字段完全相同**（名称、方向、类型），否则 FAIL |
| SG-8 | 变体清单（§8）引用的每个子图 id 必须存在于子图库；每个子图库成员必须被至少一个变体引用（**零死子图**，防止库膨胀） |

### 12.2 主图层谓词（MG-*）

| 编号 | 谓词 |
|---|---|
| MG-1 | 主图恰好 4 张，id 与 §2.2 表逐字一致；每张的 URP Target、Surface、Blend、Cull 与表一致（读 shader 的 `RenderQueue` 与 `Material.GetTag("RenderType")` 断言） |
| MG-2 | 每张主图必须暴露 §2.4 的全部 10 个统一属性（用一张探针材质 `Material.HasProperty` 逐个断言） |
| MG-3 | 主图内 `StyleStage` 槽位之后无形态 / 颜色节点。断言方式：主图配套清单声明 `styleStageIsTerminal: true`，且该声明由一条 EditMode 快照测试守护——快照记录主图的输出节点直接前驱必须是 StyleStage 槽位的三个输出端口 |
| MG-4 | `SG_VfxAdditive` 与 `SG_VfxCanvas` 的清单 `requiresVertexStage` 必须为 false，且不得引用任何 `sg_vdisp_*` |
| MG-5 | `SG_VfxCanvas` 的清单 `requiresDepthTexture` 与 `requiresOpaqueTexture` 必须均为 false |

### 12.3 材质产物层谓词（MV-*，作用于编译产物）

| 编号 | 谓词 |
|---|---|
| MV-1 | 每个 `material` 族层的材质必须有 ≥ 1 个时间驱动输入：`_LocalTime` 或 `_Progress` 或 `_BeatValue` 被至少一个启用的子图消费（由变体清单声明 `timeDriven: true` 并断言）。**例外**：变体清单显式标记 `static: true` 的变体（`材:静` 降级产物）——但此时该层必须仍有 `_Progress` 绑定（§10.4）。这是 ADR-009 "静态贴图加位移不构成特效"的本族对应物 |
| MV-2 | 每个材质恰好启用一个 `_STYLE_*` 关键字（§4.4） |
| MV-3 | 每个材质的 `_PaletteA…E` 全部已赋值且分量非负、非 NaN、非 Inf |
| MV-4 | HG-1：台阶数 ≥ 3 且相邻台阶合成亮度比 ≥ 2.0（§7.2） |
| MV-5 | HG-2：`hot` 台阶的合成亮度 ≥ 1.5（§7.4） |
| MV-6 | 材质的 shader 必须是 §2.2 的 4 张主图之一（GUID 白名单，`COMPILER_BOUNDARY_V2.md` §3）；任何其他 shader 一律拒绝 |
| MV-7 | 该层实际使用的子图 `sampleCost` 之和 ≤ 该档位的"材质噪声采样层数"上限（§2.4） |
| MV-8 | 声明了 `requiresOpaqueTexture` 的层，其材质必须同时编译出 `_REFRACT_SCENECOLOR` 开与关两个变体（读 shader 的 keyword 声明为 `multi_compile_local`，§6.2 方案 B） |
| MV-9 | `_BLEND_MULTIPLY` 材质在 2D 下的 `sortingOrder` 必须等于其配对加色边层的 `sortingOrder − 1`（§11） |

### 12.4 辉光层谓词（GL-*）

| 编号 | 谓词 |
|---|---|
| GL-1 | `Glow_k` 子节点数 == 该层 `glow.layerCount`，且 k 连续从 0 编号 |
| GL-2 | 每个 `Glow_k` 的材质 shader 必须是 `SG_VfxAdditive`，Blend 为 Additive、ZWrite 关 |
| GL-3 | 每个 `Glow_k` 的 `localScale` 必须等于 `hostRadius * glowRadius * pow(layerRadiusRatio, k)`（±1e-3），且严格随 k 递增（多层必须真的是不同半径，否则退化为单层） |
| GL-4 | 各 `Glow_k` 的 `_BreakupSeed` 两两不等（§5.5-3，防止多层凹凸对齐） |
| GL-5 | `Glow_k` 子节点上无 `Collider`、无 `MonoBehaviour`、无 `Light`（辉光是渲染不是光） |
| GL-6 | `glow.enabled` 为 true 的层，`falloffCurve` 必须解析到 4 个静态关键字之一，且恰好一个被启用 |
| GL-7 | `layerCount` ≤ §10.3 的档位上限 |

### 12.5 工程级谓词（PR-*）

| 编号 | 谓词 |
|---|---|
| PR-1 | `PlayerSettings.colorSpace == ColorSpace.Linear`（§7.1-2）。这条不通过时，全部 HG 谓词的数值语义失效，测试必须先 FAIL 在这里 |
| PR-2 | 子图库与主图库位于 `Assets/VFX/Shared/`（`VfxProjectRules.json` 的 `allowedDependencyRoots` 已含此根），且产物目录内**零个** `Shader` 资产（沿用 `VfxOutputAuditor` R8016） |

### 12.6 fail-closed 三路

与 ADR-009 §5 同构，统一在 `COMPILER_BOUNDARY_V2.md` §7.4 定义；本族的具体化：

1. **未立法子图拒绝**：子图库出现清单里没有的 `.shadersubgraph` → FAIL（SG-1）。
2. **无变体声明拒绝**：`technique.variant` 不在 §8 变体清单 → `E203`。
3. **资产不在白名单拒绝**：材质的 shader 不是 4 张主图之一 → MV-6 FAIL。

显式豁免机制：豁免表是测试内常量 `子图/变体 id → (理由, 到期卡号)`；豁免项**仍然跑谓词**，若已达标则测试失败并要求移除该豁免（防豁免表陈旧）；最终断言"已消费豁免集合恰等于声明清单"（不可静默扩缩）。

---

## 13. 审计清单

- [ ] 53 个子图（噪声 7 / SDF 9 / 阈值 5 / 边缘 5 / 流动 7 / 顶点位移 8 / 合成 9 + 直通 4 / 风格插槽 3）全部有 id / 端口 / 参数 / 服务层角色 / 2D-3D 差异 / sampleCost / minTier。
- [ ] `StyleStage` 契约 9 端口，3 个实现，接口逐字段一致（SG-7）。
- [ ] 辉光层 8 个参数（REFERENCE_ANALYSIS §3bis）全部给出实现方式，参数面 18 项三处可调。
- [ ] 折射可选层双路 + 运行时检测 + 切换方案裁定 + Queue Offset 表 + 自反射论证。
- [ ] HDR / 线性纪律 5 条 + 强度台阶的数值化谓词（HG-1/HG-2）。
- [ ] 25 个材质变体覆盖 18 个层角色。
- [ ] 六档降级：子图替换表 + 辉光截断表 + `材:静` 语义。
- [ ] 构造性谓词 31 条（SG 8 / MG 5 / MV 9 / GL 7 / PR 2）+ fail-closed 3 路。
- [ ] 正文无具体特效名；无序列帧 / flipbook / sprite sheet 任何形式；无后处理 / 相机 / UI 布局 / 全局光 / 时间缩放。
- [ ] 技术族只有 5 族，本文档只写第 1 族与其对其他族的接口。
