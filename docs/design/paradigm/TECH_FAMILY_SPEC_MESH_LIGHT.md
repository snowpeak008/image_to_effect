# 技术族实现规格：网格几何族（Procedural Mesh）+ 局部光族（Local Light）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）、`docs/design/references/REFERENCE_ANALYSIS.md`（质感标尺）
配套文档：`PROTOTYPE_CATALOG_v1.md`、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`TECH_FAMILY_SPEC_MATERIAL.md`、`TECH_FAMILY_SPEC_PARTICLES.md`、`STYLE_IMPL_CARTOON_PIXEL.md`、`COMPILER_BOUNDARY_V2.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **技术族轴** | 5 族中的第 4、5 族（网格几何 `mesh` / 局部光 `local_light`）**全部实现规格**：20 个程序化网格生成器（含 ADR-010 §4bis-8 新增的 `radial_spike_array`）+ 2 个运行时几何组件 + 描边壳附加结构、预破碎与 Rigidbody 接线、Cloth 档位、顶点位移与材质族的分工边界、局部光节拍器组件规格（参数面 / 风格量化 / `Light` 与 `Light2D` 双驱动 / `光:烘` 退化 / 与辉光层 `flickerCoupling` 的同步）、构造性谓词 |
| **原型轴** | 不新增、不修改原型；网格族服务 21 个层角色中的 **13 个**（`core / trail / ground / shock / beam_column / link / mesh_shell / debris / surface / orbit / column / veil / cloth`）；局部光族服务 `light` 角色（58 原型中 98 处，是必需层最多的角色） |
| **元素轴** | 13 元素 + `none` 的 5 个物理子画像在两族的预设倾向全部有生成器 / 光预设归属（§7 与 §6.8 速查表） |
| **风格轴** | 首批 2 的几何约束（`STYLE_CATALOG_v1.md` §2.4 / §3.4）与光约束（§2.5 / §3.5）落到具体实现位置：细分上限 / 描边壳 / 位移量化 / 光强色阶 / 闪烁量化 / 阴影许可 |
| **维度轴** | 每个生成器给出 2D-3D 差异；局部光给出 `Light`（3D）/ `Light2D`（2D）双驱动 |
| **档位轴** | 六档全部有降级机制：每生成器给六档顶点预算（§4 各行）；Cloth 六档（§4.18）；破碎块六档（§4.17）；局部光六档含 `光:烘`（§6.6） |

**纪律**：正文只用层角色 / 技术族 / 参数表述，无具体特效名；几何一律 Unity 基础几何或脚本程序化生成（ADR-010 §5），**零外部模型导入**；局部光仅指预制体子节点上的 Point / Spot / Light2D，不碰场景全局光。

---

## 1. 网格几何族的职责与产物形状

### 1.1 职责

材质族回答"形态如何被算出来"，网格族回答"**这个形态需要真实的顶点吗**"。需要真实顶点的三种情形：

1. **需要深度与遮挡**：实体壳、碎块、柱体——它们要能被自己和别的东西遮挡，quad 做不到。
2. **需要拓扑**：折线、样条带、环段、尾迹——形状的连通关系本身是信息，SDF 无法表达"这条链有 5 段"。
3. **需要物理**：刚体碎块、Cloth——物理引擎作用于顶点/碰撞体。

不属于这三种的一律用材质族的 SDF（成本更低、风格量化更统一）。分工边界的完整判据见 §5。

### 1.2 产物形状

```
Layers/<layerId>                        GameObject
├─ MeshFilter（mesh = 编译期生成并写入产物目录的 .asset）
├─ MeshRenderer（material 来自材质族）
├─ 可选：Rigidbody + Collider（预破碎块，§4.17）
├─ 可选：Cloth（§4.18）
├─ 可选：TrailRenderer / LineRenderer（§4.20 / §4.21）
├─ 可选：MeshRenderer#2（`Cull Front` 描边壳，卡通 3D，§4.22）
└─ 可选：Glow_k 子节点（材质族 §5）
```

### 1.3 生成器的执行时机：编译期，不是运行时

**全部网格在编译期生成并作为 `.asset` 写入产物目录**（`Assets/VFX/Generated/<id>/Meshes/`）。理由三条：

1. **确定性**：`seed` + 生成器版本 → 字节相同的网格。运行时生成会引入初始化开销与平台差异，破坏 `RECIPE_V2_SCHEMA_DRAFT.md` §1-6 的确定性承诺。
2. **可断言**：顶点数、包围盒、拓扑可以在 EditMode 直接断言（§8 谓词），运行时生成只能在 PlayMode 断言。
3. **预算诚实**：`PROTOTYPE_CATALOG_v1.md` §2.4 的"程序化网格顶点/层"上限只有在资产化后才是真实可查的数字。

**例外（唯一）**：`TrailRenderer` / `LineRenderer` 的几何由 Unity 运行时生成，它们不是"生成器产物"，是组件行为。见 §4.20 / §4.21。

**运行时形变一律走顶点位移**（材质族的 `sg_vdisp_*`，§5），不重建网格。

### 1.4 生成器的统一契约

每个生成器是一个 Editor 侧静态方法，签名统一：

```
Mesh Generate(GeneratorParams p, uint seed, Dimension dim, VertexBudget budget)
```

- 输出网格必须：有 `normals`、有 `uv0`（形态 UV）、有 `uv1`（**沿结构的参数化**：如沿链的弧长、沿柱的高度、沿环的角度——材质族的阈值推进与流动依赖它）、有 `colors`（`r` = 结构随机值、`g` = 到边界的归一化距离、`b` = 段/块 id 归一化、`a` = 备用）。
- 顶点数 ≤ `budget`（档位给出），超出时生成器**自行降细分**并在返回值中报告实际值（不是抛错）。
- 结果必须是确定性的：同 `(params, seed, dim, budget)` → 逐字节相同。

`uv1` 与 `colors` 的这套约定是网格族与材质族的接口；没有它，"元素预设一次做好所有原型自动获得"在几何层就断了。

---

## 2. 生成器清单读表约定

- **顶点数量级**：典型参数下的量级，不是硬上限。
- **六档顶点预算**：`PROTOTYPE_CATALOG_v1.md` §2.4 给出的是"程序化网格顶点/层"的档位上限（ML ≤ 256 / MM ≤ 512 / MH ≤ 2k / PL ≤ 2k / PM ≤ 8k / PH ≤ 32k）。每个生成器给出在各档下的**主参数取值**，使顶点数落在上限内。
- **2D-3D 差异**：2D 下多数生成器产出平面网格（Z=0）并交由 Sorting 排序；写"同"表示两维一致。

---

## 3. 生成器分类总览

| 类 | 生成器 | 数 |
|---|---|---|
| 簇 / 块（离散实体） | `crystal_cluster` · `rock_chunk` · `tendril_blob` · `shell_polyhedron` · `wire_polyhedron` | 5 |
| 线 / 带（拓扑主导） | `jagged_polyline` · `spiral_ribbon` · `sweep_band` · `catenary_band` · `vine_spline` · `branch_tree` · `parabola_tube` | 7 |
| 环 / 柱（旋转体） | `ring_torus_segments` · `cylinder_beam` · `radial_spike_array` | 3 |
| 面 / 板 | `subdivided_plane` · `tech_panel` · `splash_crown` | 3 |
| 破碎 / 物理 | `voronoi_prefracture` · `cloth_patch` | 2 |
| **生成器合计** | | **20** |
| 运行时组件（非生成器，本文档一并规格化） | `trail_mesh` · `line_mesh` | 2 |
| 编译期附加结构（非生成器） | 描边壳 `outline_shell`（§4.22） | 1 |

`radial_spike_array` 是 ADR-010 §4bis-8 裁定新增（T2a 层词表未覆盖），见 §4.19；`splash_crown`（§4.23）补齐 T2A_REPORT §5.1-3 清单中的液体冠形。20 个生成器覆盖 T2A_REPORT §5.1-3 建议的 18 个（`crystal_cluster` / `rock_chunk` / `jagged_polyline` / `spiral_ribbon` / `tendril_blob` / `wire_polyhedron` / `tech_panel` / `vine_spline` / `branch_tree` / `sweep_band` / `catenary_band` / `parabola_tube` / `splash_crown` / `shell_polyhedron` / `ring_torus_segments` / `cylinder_beam` / `voronoi_prefracture` / `subdivided_plane`）+ 新增 2 个（`radial_spike_array` / `cloth_patch`）。

---

## 4. 程序化网格生成器逐个规格

### 4.1 `crystal_cluster` — 晶体簇

| 项 | 内容 |
|---|---|
| **输入参数** | `shardCount i 3~24`、`lengthRange f2`、`radiusRange f2`、`sides i 4~8`（棱柱边数，6 = 六方）、`tipRatio f 0~1`（收尖比例）、`spreadCone f`（从中心向外的锥角）、`baseRadius f`（生长起点半径）、`alignment f 0~1`（0=完全随机朝向，1=严格沿锥轴） |
| **顶点数量级** | `shardCount * (sides * 2 + 1)`，典型 8 根 × 六方 ≈ 104 |
| **生成算法要点** | ① 在 `baseRadius` 球面（3D）/ 圆周（2D）上按 Halton 序列（确定性、分布均匀）取 `shardCount` 个起点；② 每根方向 = `lerp(随机半球方向, 锥轴, alignment)`；③ 沿方向生成棱柱：底面 `sides` 边正多边形，顶面按 `tipRatio` 收缩（`tipRatio=0` 出尖锥）；④ 侧面 flat-shaded（**不做法线平滑**——硬边靠几何而非贴图，REFERENCE_ANALYSIS §1-B）；⑤ `uv1.x` = 沿棱柱高度，`colors.b` = 棱柱 id 归一化（供逐根随机相位） |
| **服务层角色** | `core / mesh_shell / column / debris / orbit` |
| **六档顶点预算** | ML: `shardCount=3, sides=4` (≈39) / MM: `5,4` (≈45) / MH: `8,6` (≈104) / PL: 同 MH / PM: `16,6` (≈208) / PH: `24,8` (≈408) |
| **2D-3D** | 2D：棱柱退化为等腰梯形/三角形平面片，起点在圆周上，按 `colors.b` 分前后两组供 Sorting |

### 4.2 `rock_chunk` — 不规则岩块

| 项 | 内容 |
|---|---|
| **输入参数** | `subdivisions i 0~3`、`roughness f 0~1`、`lowFreqAmp f`、`highFreqAmp f`、`flatnessBias f 0~1`（趋向板状）、`facetAngle f`（法线硬化角阈值） |
| **顶点数量级** | 二十面体细分：`20 * 4^subdiv * 3` 顶点（flat-shaded 不共享），subdiv=1 ≈ 240 |
| **生成算法要点** | ① 从正二十面体开始，`subdivisions` 次 Loop 细分并球面归一化；② 顶点沿法线加两层 value noise（`lowFreqAmp` 大尺度不规则 + `highFreqAmp` 粗糙）；③ `flatnessBias` 沿一个随机轴做非均匀缩放；④ **flat-shaded**：每三角独立顶点，法线取面法线（岩石的硬棱角来自这一步）；⑤ `colors.g` = 顶点到原始球心距离的归一化（供材质做"凸起更亮"） |
| **服务层角色** | `core / debris / column / mesh_shell` |
| **六档** | ML: `subdiv=0` (60) / MM: `0` + 更强噪声 / MH: `1` (240) / PL: 同 / PM: `2` (960) / PH: `3` (3840) |
| **2D-3D** | 2D：从随机凸多边形（8~14 边）出发，边中点加噪声后三角化为扇形，flat 法线全为 `-Z` |

### 4.3 `tendril_blob` — 触手团

| 项 | 内容 |
|---|---|
| **输入参数** | `tendrilCount i 3~12`、`bodyRadius f`、`tendrilLength f2`、`tendrilThickness f2`、`segmentsPerTendril i 3~8`、`curl f`（卷曲量）、`taper f` |
| **顶点数量级** | `球体基底(≈80) + tendrilCount * segments * ringVerts(6)`，典型 6 根 × 5 段 ≈ 260 |
| **生成算法要点** | ① 低细分 UV 球作躯干；② 在球面取 `tendrilCount` 个 Halton 点，每点长出一条管：沿"初始法线 + 逐段 `curl` 旋转"的路径推进，每段一个 6 边形环，半径按 `taper` 收缩；③ 管与躯干的接缝用一圈过渡环焊接（避免破面）；④ `uv1.x` = 沿触手弧长（供"从根到梢"的阈值推进），`colors.b` = 触手 id |
| **服务层角色** | `core / mesh_shell / body / link` |
| **六档** | ML: `3 根 × 3 段` (≈134) / MM: `4×4` / MH: `6×5` (≈260) / PL: 同 / PM: `10×6` / PH: `12×8` |
| **2D-3D** | 2D：躯干为圆形扇面，触手为带状条（每段一个四边形） |

### 4.4 `shell_polyhedron` — 多面体壳

| 项 | 内容 |
|---|---|
| **输入参数** | `baseShape i`（`icosphere / geodesic / cube / cylinder`）、`subdivisions i 0~4`、`facetMode b`（true=平面刻面 / false=平滑）、`openTop f 0~1`（顶部开口比例，做半球罩）、`thickness f`（0=单面壳，>0=双面带厚度） |
| **顶点数量级** | icosphere subdiv=2 ≈ 320（平滑）/ 960（刻面） |
| **生成算法要点** | ① 基底球/柱生成；② `facetMode` 时每三角独立顶点 + 面法线（**这是"晶格壳"读感的来源**，配合材质的 Voronoi 可产生双层刻面）；③ `openTop` 按极角裁剪；④ `thickness > 0` 时沿法线内推复制一层并缝合边界；⑤ `uv1.x` = 极角，`uv1.y` = 方位角（供材质做球面极坐标流动），`colors.b` = 面 id（供逐面点亮/脱落） |
| **服务层角色** | `mesh_shell / core / veil / barrier 类语义` |
| **六档** | ML: `subdiv=0` 平滑 (42) / MM: `subdiv=1` 平滑 (162) / MH: `subdiv=2` 平滑 (320) / PL: 同 / PM: `subdiv=2` 刻面 (960) / PH: `subdiv=3` 刻面 (3840) |
| **2D-3D** | 2D：退化为圆环带（内外两圈顶点），`openTop` 变成弧段裁剪 |

### 4.5 `wire_polyhedron` — 框线多面体

| 项 | 内容 |
|---|---|
| **输入参数** | `solid i`（`tetra / cube / octa / dodeca / icosa`）、`wireRadius f`、`wireSides i 3~6`、`nodeStyle i`（`none / sphere / cube`）、`nodeScale f` |
| **顶点数量级** | `edgeCount * wireSides * 2 + nodeCount * nodeVerts`；正八面体 12 边 × 4 侧 ≈ 96 |
| **生成算法要点** | ① 取柏拉图立体的顶点/边表（**硬编码常量表**，5 个立体的顶点与边索引，无需运行时计算）；② 每条边生成一根管（`wireSides` 边形截面，两端封口或接节点）；③ `nodeStyle != none` 时在每个顶点放一个小几何；④ `uv1.x` = 沿边的参数（供流光沿边跑），`colors.b` = 边 id |
| **服务层角色** | `orbit / mesh_shell.cage / core / link` |
| **六档** | ML: `tetra, wireSides=3` (36) / MM: `octa, 3` (72) / MH: `icosa, 4` (240) / PL: 同 / PM: `dodeca, 5` (≈300) / PH: `dodeca, 6` + 节点 (≈600) |
| **2D-3D** | 2D：退化为正 N 边形线框（N 由 `solid` 映射），边为带状四边形 |

### 4.6 `jagged_polyline` — 锯齿折线（递归分叉）

| 项 | 内容 |
|---|---|
| **输入参数** | `startPoint f3`、`endPoint f3`、`segments i 4~64`、`jitter f`（垂直于主轴的偏移幅度）、`jitterFalloff f`（端点处偏移趋 0）、`branchDepth i 0~3`、`branchProbability f 0~1`、`branchLengthRatio f 0.2~0.7`、`branchAngleRange f2`、`bandWidth f`、`billboardMode b`（true=面向相机的带，false=固定平面带） |
| **顶点数量级** | 主线 `segments*2` + 分叉；`segments=16, depth=2` ≈ 90 |
| **生成算法要点** | ① 主线用**中点位移法**（midpoint displacement）：递归把线段中点沿垂直方向偏移 `jitter * 2^(-level)`，得到自相似锯齿；② 端点处偏移乘 `jitterFalloff`（保证两端严格锚在给定点上——这是 `link` 类层的接口要求）；③ 每层递归中按 `branchProbability` 在节点处派生子线（长度 = 父长 × `branchLengthRatio`，角度在 `branchAngleRange` 内），递归 `branchDepth` 层；④ 每条线膨胀为带：`billboardMode` 时只生成中心线 + 侧向偏移属性交给顶点着色器做相机对齐，否则直接在固定平面展开；⑤ `uv1.x` = 沿线归一化弧长（**全局连续**，分叉子线继承父线的起始弧长，供"电流沿链跑"的阈值推进），`colors.b` = 分叉深度归一化 |
| **服务层角色** | `link / beam_column / trail / edge / shock` |
| **六档** | ML: `segments=6, depth=0` (14) / MM: `8, 1` (≈26) / MH: `16, 2` (≈90) / PL: 同 / PM: `32, 3` (≈300) / PH: `64, 3` (≈600) |
| **2D-3D** | 3D：偏移在垂直于主轴的随机平面内；2D：偏移限于 XY 平面法向。`billboardMode` 在 2D 恒为 false |

### 4.7 `spiral_ribbon` — 螺旋带

| 项 | 内容 |
|---|---|
| **输入参数** | `turns f 0.5~8`、`radiusStart f`、`radiusEnd f`、`heightStart f`、`heightEnd f`、`ribbonWidth f`、`widthCurve i`、`segmentsPerTurn i 8~48`、`twist f`（带自身的扭转角总量） |
| **顶点数量级** | `turns * segmentsPerTurn * 2`；3 圈 × 16 ≈ 96 |
| **生成算法要点** | ① 沿螺旋线采样 `turns * segmentsPerTurn` 个点（半径与高度线性插值，可加 `easeCurve`）；② 每点的局部坐标系：切线 = 螺旋切向，法线 = 径向，副法线 = 二者叉积；③ 带宽沿 `widthCurve` 变化，带的朝向按 `twist` 逐点旋转；④ `uv1.x` = 沿螺旋弧长，`uv1.y` = 带宽方向 |
| **服务层角色** | `body / trail / column / link / orbit / veil` |
| **六档** | ML: `2 圈 × 8` (32) / MM: `2×12` (48) / MH: `3×16` (96) / PL: 同 / PM: `5×24` (240) / PH: `8×48` (768) |
| **2D-3D** | 2D：阿基米德螺线（平面内），`heightStart/End` 忽略 |

### 4.8 `sweep_band` — 扫掠面

| 项 | 内容 |
|---|---|
| **输入参数** | `arcAngle f`（扫掠总角度）、`innerRadius f`、`outerRadius f`、`segments i 8~64`、`thicknessProfile i`（沿弧的宽度剖面：`even / leadHeavy / tailHeavy / lens`）、`tilt f`（扫掠平面倾角）、`twistAlongArc f`、`layerCount i 1~3`（多层错位带，制造厚度） |
| **顶点数量级** | `segments * 2 * layerCount`；32 段 × 2 层 = 128 |
| **生成算法要点** | ① 沿弧采样，每点生成内外两个顶点；② `thicknessProfile` 调制内外半径差；③ `layerCount > 1` 时复制多层，每层沿弧向相位偏移 `k * phaseStep`、半径微缩、法线微偏——**这是"沿运动轴拉伸的丝带/条纹结构"的几何落点**（REFERENCE_ANALYSIS §1-A：主体形态用条带网格而非单一 billboard）；④ `uv1.x` = 沿弧归一化（供"从起点扫到终点"的阈值推进），`uv1.y` = 内外向，`colors.b` = 层 id |
| **服务层角色** | `core / trail / shock / veil / body / edge` |
| **六档** | ML: `16 段 × 1 层` (32) / MM: `24×1` (48) / MH: `32×2` (128) / PL: 同 / PM: `48×3` (288) / PH: `64×3` (384) |
| **2D-3D** | 2D：扫掠平面即 XY 平面，`tilt` 忽略；多层用 Sorting 分前后 |

### 4.9 `catenary_band` — 悬链带

| 项 | 内容 |
|---|---|
| **输入参数** | `pointA f3`、`pointB f3`、`sag f 0~1`（下垂量，0=直线）、`segments i 4~48`、`bandWidth f`、`widthCurve i`、`twist f`、`slackNoise f`（沿链的随机松弛） |
| **顶点数量级** | `segments * 2`；24 段 = 48 |
| **生成算法要点** | ① 两端点之间按悬链线 `y = a*cosh(x/a)` 采样（`a` 由 `sag` 反解），退化时用二次贝塞尔；② 带朝向：`billboardMode` 或固定平面；③ `slackNoise` 沿链加低频扰动；④ `uv1.x` = 沿链弧长，两端严格锚定 |
| **服务层角色** | `link / trail / beam_column` |
| **六档** | ML: 8 段 (16) / MM: 12 (24) / MH: 24 (48) / PL: 同 / PM: 36 (72) / PH: 48 (96) |
| **2D-3D** | 同（2D 时下垂方向为 -Y） |

### 4.10 `vine_spline` — 藤蔓样条

| 项 | 内容 |
|---|---|
| **输入参数** | `controlPoints f3[]` 或 `(start, end, wanderAmplitude, wanderFreq)`、`segments i 8~64`、`tubeRadius f`、`tubeSides i 3~8`、`taper f`、`leafCount i 0~24`、`leafSize f`、`leafAngleRange f2`、`coilAround b`（是否螺旋缠绕主轴） |
| **顶点数量级** | `segments * tubeSides + leafCount * 4`；32 段 × 5 边 + 12 叶 ≈ 208 |
| **生成算法要点** | ① 样条：Catmull-Rom 过控制点，或由 `wander` 参数程序化生成（起点到终点 + 低频噪声偏移）；② 沿样条扫掠 `tubeSides` 边形截面，半径按 `taper` 收缩；③ `coilAround` 时样条本身绕主轴螺旋；④ 叶片：沿样条按 Halton 取 `leafCount` 个位置，每片一个 quad，朝向 = 样条法线绕切线随机旋转，形状由材质的 `sg_sdf_leaf_petal` 给（**quad 上的程序化叶形，不是叶片贴图**）；⑤ `uv1.x` = 沿藤弧长（供"从根向梢生长"的阈值推进） |
| **服务层角色** | `link / mesh_shell.cage / column / trail / core` |
| **六档** | ML: `12 段 × 3 边, 0 叶` (36) / MM: `16×3, 4 叶` (64) / MH: `32×5, 12 叶` (208) / PL: 同 / PM: `48×6, 20 叶` (368) / PH: `64×8, 24 叶` (608) |
| **2D-3D** | 2D：管退化为带（2 顶点/段），叶为平面 quad，前后 Sorting 分组 |

### 4.11 `branch_tree` — 根系 / 树状分叉

| 项 | 内容 |
|---|---|
| **输入参数** | `depth i 1~5`、`branchesPerNode i 2~4`、`lengthRatio f 0.4~0.8`、`radiusRatio f 0.5~0.8`、`angleSpread f`、`rootRadius f`、`rootLength f`、`tubeSides i 3~6`、`planarBias f 0~1`（趋向平面，1=完全共面，用于贴地根系） |
| **顶点数量级** | 分支数 = `Σ branchesPerNode^k`；depth=3, b=2 → 15 段 × 4 边 ≈ 120 |
| **生成算法要点** | ① 递归：从根段出发，每层末端派生 `branchesPerNode` 个子段，长度 × `lengthRatio`，半径 × `radiusRatio`，方向在父方向周围 `angleSpread` 锥内按 Halton 分布；② `planarBias` 把子段方向向一个固定平面投影插值；③ 每段一根管；④ `uv1.x` = **从根到当前点的累计弧长归一化**（全树统一尺度，供生长阈值从根推进到全部末梢），`colors.b` = 深度归一化 |
| **服务层角色** | `link / ground / decal / mesh_shell / column` |
| **六档** | ML: `depth=2, b=2, sides=3` (≈42) / MM: `depth=2, b=3, sides=3` (≈78) / MH: `depth=3, b=2, sides=4` (≈120) / PL: 同 / PM: `depth=4, b=3, sides=4` (≈480) / PH: `depth=5, b=3, sides=6` (≈2900) |
| **2D-3D** | 2D：`planarBias` 恒为 1，管退化为带 |

### 4.12 `parabola_tube` — 抛物管（流）

| 项 | 内容 |
|---|---|
| **输入参数** | `origin f3`、`initialVelocity f3`、`gravity f`、`duration f`、`segments i 8~48`、`radiusStart f`、`radiusEnd f`、`tubeSides i 3~8`、`pulseAmplitude f`、`pulseFreq f`（截面沿管的脉冲变化） |
| **顶点数量级** | `segments * tubeSides`；24 × 6 = 144 |
| **生成算法要点** | ① 按抛体运动采样路径点；② 沿路径扫掠圆截面，半径从 `radiusStart` 到 `radiusEnd`（模拟流速加快时的收窄），叠加 `pulseAmplitude * sin(u * pulseFreq)`；③ `uv1.x` = 沿管归一化（供流动与"流头推进"），`uv1.y` = 周向 |
| **服务层角色** | `link.stream / beam_column / trail / column` |
| **六档** | ML: `8 段 × 3 边` (24) / MM: `12×4` (48) / MH: `24×6` (144) / PL: 同 / PM: `36×8` (288) / PH: `48×8` (384) |
| **2D-3D** | 2D：管退化为带，宽度即直径 |

### 4.13 `ring_torus_segments` — 环 / 分段环

| 项 | 内容 |
|---|---|
| **输入参数** | `majorRadius f`、`minorRadius f`、`majorSegments i 8~64`、`minorSegments i 3~12`、`segmentCount i 1~24`（把环切成 N 段）、`segmentGap f 0~0.5`、`flatRing b`（true=扁平环带而非环面）、`taperPerSegment f` |
| **顶点数量级** | `majorSegments * minorSegments`；32 × 6 = 192；`flatRing` 时 `majorSegments * 2` |
| **生成算法要点** | ① 标准环面参数化；② `segmentCount > 1` 时按角度分段并按 `segmentGap` 留缝（每段独立封端）；③ `flatRing` 时只生成内外两圈顶点（薄环带，2D 与低档主用）；④ `uv1.x` = 周向角度归一化，`colors.b` = 段 id（供逐段点亮） |
| **服务层角色** | `shock / mesh_shell.cage / orbit / ground / edge / column` |
| **六档** | ML: `flatRing, 16 段` (32) / MM: `flatRing, 24` (48) / MH: `32×6` (192) / PL: 同 / PM: `48×8` (384) / PH: `64×12` (768) |
| **2D-3D** | 2D 恒为 `flatRing`（或直接用材质极坐标环，见 §5 分工判据） |

### 4.14 `cylinder_beam` — 柱体束

| 项 | 内容 |
|---|---|
| **输入参数** | `length f`、`radiusStart f`、`radiusEnd f`、`radialSegments i 6~32`、`heightSegments i 1~24`、`capMode i`（`none / flat / dome / taper`）、`bulgeProfile i`、`crossSection i`（`circle / hexagon / square / star`）、`billboardPair b`（true=用两片交叉 quad 代替真柱，低档用） |
| **顶点数量级** | `radialSegments * (heightSegments+1)`；12 × 9 = 108；`billboardPair` = 8 |
| **生成算法要点** | ① 截面按 `crossSection` 生成 N 边形；② 沿高度采样，半径按 `radiusStart→radiusEnd` + `bulgeProfile` 调制；③ 端盖按 `capMode`；④ `heightSegments > 1` 是为了让 `sg_vdisp_sway_bend` 有顶点可弯；⑤ `uv1.y` = 沿高度归一化（供"从下往上生长"），`uv1.x` = 周向 |
| **服务层角色** | `beam_column / column / core / link / mesh_shell` |
| **六档** | ML: `billboardPair` (8) / MM: `6 × 3` (24) / MH: `12 × 9` (108) / PL: 同 / PM: `20 × 16` (340) / PH: `32 × 24` (800) |
| **2D-3D** | 2D：两片交叉 quad 无意义，直接单 quad（`billboardPair=false` 时用带状矩形 + 高度细分供弯曲） |

### 4.15 `subdivided_plane` — 细分平面

| 项 | 内容 |
|---|---|
| **输入参数** | `size f2`、`subdivX i 1~128`、`subdivY i 1~128`、`shape i`（`rect / disc / roundedRect / customSdfMask`）、`skirtHeight f`（边缘下垂裙边，避免边界硬切）、`uvMode i`（`stretch / tile / radial`） |
| **顶点数量级** | `(subdivX+1) * (subdivY+1)`；16×16 = 289 |
| **生成算法要点** | ① 规则网格；② `shape != rect` 时按 SDF 裁剪并在边界处生成一圈贴合边界的顶点（避免锯齿边）；③ `skirtHeight > 0` 时沿边界向下挤出一圈；④ `uv1` 按 `uvMode`；⑤ **这是 Cloth 与顶点位移表面场的基底**，因此顶点必须均匀分布（Cloth 的约束求解对不均匀网格不稳定） |
| **服务层角色** | `surface / ground / veil / cloth / decal` |
| **六档** | ML: `8×8` (81) / MM: `12×12` (169) / MH: `16×16` (289) / PL: 同 / PM: `48×48` (2401) / PH: `96×96` (9409) |
| **2D-3D** | 同（2D 时平面即 XY） |

### 4.16 `tech_panel` — 板 / 格拼接壳

| 项 | 内容 |
|---|---|
| **输入参数** | `gridType i`（`square / hex / tri`）、`cols i`、`rows i`、`cellGap f`、`bevel f`、`thickness f`、`curvature f 0~1`（0=平板，1=完全贴合球面）、`missingRatio f 0~1`（随机缺格） |
| **顶点数量级** | `cols * rows * versPerCell`；8×8 六边格 × 6 顶点 ≈ 384 |
| **生成算法要点** | ① 按 `gridType` 铺格，每格独立（不共享顶点——这是"逐格脱落"的前提）；② `bevel > 0` 时每格边缘内缩生成倒角环；③ `thickness > 0` 时挤出；④ `curvature` 把整块投影到球面；⑤ `missingRatio` 按 `seed` 确定性剔除若干格；⑥ `colors.b` = 格 id 归一化，`colors.r` = 格中心到板中心的归一化距离（供从中心向外的逐格点亮） |
| **服务层角色** | `mesh_shell / surface / veil / debris / fill` |
| **六档** | ML: `4×4 square, 无 bevel/thickness` (64) / MM: `6×6` (144) / MH: `8×8 hex + bevel` (384) / PL: 同 / PM: `12×12 hex + thickness` (≈1700) / PH: `20×20` (≈4800) |
| **2D-3D** | 2D：`curvature`/`thickness` 忽略，纯平面格 |

### 4.17 `voronoi_prefracture` — 预破碎

| 项 | 内容 |
|---|---|
| **输入参数** | `sourceShape i`（`sphere / box / cylinder / plane / fromGenerator:<id>`）、`fragmentCount i 4~200`、`sitePattern i`（`uniform / clustered / radialFromPoint / planar`）、`impactPoint f3`（`radialFromPoint` 时的中心）、`clusterTightness f`、`innerFaceInset f`（断面内缩，避免 z-fighting）、`colliderMode i`（`box / convexHull / none`）、`massMode i`（`byVolume / uniform`） |
| **顶点数量级** | 全部碎块之和 ≈ `fragmentCount * 30`（凸多面体平均 30 顶点）；48 块 ≈ 1440。**每块是独立子节点，各自计入顶点预算的是"层总和"** |
| **生成算法要点** | ① 在源形状包围盒内按 `sitePattern` 生成 `fragmentCount` 个站点（`radialFromPoint` 时密度随距 `impactPoint` 增大而降低——命中点附近碎得更细，这是"破碎看起来对"的关键）；② 计算 3D Voronoi 胞（用半空间裁剪法：每个胞 = 源形状被所有"站点对中垂面"依次裁剪的结果，凸多面体裁剪是稳定的确定性算法）；③ 每胞独立网格，断面法线朝外，断面顶点按 `innerFaceInset` 内缩；④ 断面标记：`colors.a = 1`（原表面 `colors.a = 0`）——材质用它给断面单独的外观（元素目录多处要求"断面加余烬/纯色/同色稍暗"）；⑤ `colliderMode` 生成对应碰撞体；⑥ 每块的质心、体积写入块清单 asset（供运行时施加爆炸力） |
| **服务层角色** | `debris / mesh_shell.crack / core` |
| **六档块数** | ML: 8（**无刚体**，脚本/材质位移）/ MM: 16（简化碰撞：`box`）/ MH: 32 / PL: 48 / PM: 96 / PH: 200。与 `PROTOTYPE_CATALOG_v1.md` §2.4 一致 |
| **2D-3D** | 2D：二维 Voronoi + `PolygonCollider2D`，碎块为三角化的多边形片（`ELEMENT_CATALOG_v1.md` 多处的"Sprite 网格三角切片"） |

**Rigidbody 接线规格（详见 §4.17b）**

### 4.17b 预破碎与 Rigidbody 的接线

| 项 | 规格 |
|---|---|
| **层次结构** | `Layers/<layerId>` （父，无 Rigidbody）→ `Frag_000 … Frag_{n-1}`（每块一个子节点：MeshFilter + MeshRenderer + Collider + Rigidbody） |
| **初始状态** | 全部 Rigidbody `isKinematic = true`，`useGravity = false`，子节点 `activeSelf = true` 但材质 alpha 由整壳材质统一控制（未破碎时碎块拼成完整形状，视觉上等同一个整壳）。**不用"破碎时才实例化"**：实例化 200 个刚体会造成明显卡顿，且违反"确定性"（实例化顺序影响物理） |
| **触发** | 控制器的 `break` 入事件 → 遍历碎块：`isKinematic = false`、`useGravity = 按 gravityScale`、`AddExplosionForce(breakForce, impactPoint, radius, upwardModifier)` + `AddTorque(随机, 由 seed 确定)` |
| **物理层（Layer）** | 碎块**不改 GameObject 的 layer**（layer 是用户工程的语义，特效不得占用）。碰撞筛选靠 `Rigidbody.excludeLayers` / `Collider.excludeLayers`（Unity 2022+ 的组件级排除），默认排除 nothing（与用户场景正常碰撞）。若用户希望碎块不碰撞，通过接口参数 `debrisCollides` 关闭 → 编译期设 `Collider.enabled = false` 并改为纯运动学脚本轨迹 |
| **休眠与回收** | `settleTime` 后（或全部刚体 `IsSleeping()`）触发 `onSettled` 出事件；随后按 `fadeMode`（`dissolve` 阈值消散 / `sink` 下沉 / `hold` 保留）淡出。淡出完成 → `isKinematic = true` 并复位到初始局部变换（池化复位，§`COMPILER_BOUNDARY_V2.md` §5.5） |
| **ML 档（无刚体）** | 全部碎块 `Rigidbody` **不生成**，改为控制器驱动的确定性轨迹：每块按 `seed` 派生一个初速与角速度，逐帧 `pos += v*dt; v += g*dt`，无碰撞。这保证 ML 档视觉上仍是"块飞出去"，只是不与场景交互 |
| **2D** | `Rigidbody2D` + `PolygonCollider2D`，`AddForce` 沿径向 + `AddTorque` |
| **预算** | 碎块的 Rigidbody 数计入成本模型的"破碎块"项（`COMPILER_BOUNDARY_V2.md` §4.2）；碎块 GameObject 数计入结构预算，因此 F 类以外的原型的 `MaxGameObjects` 需为破碎层单独放宽（见 `COMPILER_BOUNDARY_V2.md` §4.2 的分项预算） |

### 4.18 `cloth_patch` — 布料片

| 项 | 内容 |
|---|---|
| **输入参数** | `size f2`、`subdivX i`、`subdivY i`、`pinMode i`（`topEdge / corners / topCenter / custom`）、`pinIndices i[]`、`shape i`（`rect / trapezoid / cape / customSdfMask`）、`doubleSided b` |
| **顶点数量级** | `(subdivX+1)*(subdivY+1)`；16×16 = 289 |
| **生成算法要点** | ① 基于 `subdivided_plane`；② 生成 `ClothSkinningCoefficient` 数组：`pinMode` 决定哪些顶点 `maxDistance = 0`（完全固定），其余按到固定点的距离线性放开；③ `doubleSided` 时不复制几何，改由材质双面渲染（Cull Off）——复制会让 Cloth 求解顶点数翻倍 |
| **服务层角色** | `cloth / veil` |
| **六档（Cloth 档位）** | ML: **禁用 Cloth**，用 `sg_vdisp_sway_bend` + `sg_vdisp_gerstner` 顶点位移波替代（8×8 网格）/ MM: 禁用，同 ML（12×12）/ MH: Cloth 低分辨率（12×12，`solverFrequency=60`，`useTethers=true`，无自碰撞）/ PL: Cloth 低（16×16，`solverFrequency=90`）/ PM: Cloth 中（24×24，`solverFrequency=120`，`selfCollisionDistance` 开）/ PH: Cloth 高（32×32，`solverFrequency=180`，自碰撞 + `capsuleColliders` 支持）。与 `PROTOTYPE_CATALOG_v1.md` §2.4 的 Cloth 行一致 |
| **Cloth 组件接线** | `Cloth.coefficients` 由生成器产出并在编译期写入；`Cloth.capsuleColliders` / `sphereColliders` **默认为空**（对外部零假设——特效不知道用户角色的碰撞体）；接口参数 `clothColliders`（`transformRef[]`）允许用户运行时接入，为空时布料只受重力与风 |
| **风** | `Cloth.externalAcceleration` / `randomAcceleration` 由控制器按 recipe 的 `windVector` / `windTurbulence` 参数写入。**不读场景风区**（`WindZone` 是场景对象） |
| **2D-3D** | 2D：Cloth 组件不可用（Unity Cloth 是 3D 组件），恒用顶点位移波替代。这与 `PROTOTYPE_CATALOG_v1.md` §2.5 的通则一致 |

### 4.19 `radial_spike_array` — 放射光针阵列（ADR-010 §4bis-8 新增）

| 项 | 内容 |
|---|---|
| **来源** | ADR-010 §4bis 第 8 条用户裁定：参考 B 组关键特征（从中心向外的细长高亮射线），T2a 层词表未覆盖，T2b 补入。 |
| **输入参数** | `spikeCount i 4~96`、`lengthRange f2`（每根长度的随机范围）、`widthRange f2`（根部宽度范围）、`taper f 0~1`（锥度：1=尖到零宽，0=等宽条）、`angleJitter f 0~1`（方向随机度：0=严格均匀分布，1=完全随机）、`coneAngle f 0~180`（限制在锥内，180=全向）、`coneAxis f3`、`originRadius f`（起点离中心的距离，>0 时针从一个球/圆面上长出而非一点）、`lengthDistribution i`（`uniform / bimodal / powerLaw`——`bimodal` 出"少数极长 + 多数中等"的层次，这是参考图里放射光针的读感来源）、`billboardMode b`（true=每根面向相机的 quad，false=固定平面四边形）、`doubleQuad b`（true=每根用两片十字交叉 quad，无论视角都有厚度） |
| **顶点数量级** | `spikeCount * 4 * (doubleQuad ? 2 : 1)`；32 根单片 = 128 顶点 |
| **生成算法要点** | ① 方向：在 `coneAngle` 锥内取 `spikeCount` 个方向。基准用**球面 Fibonacci 分布**（3D）/ 等角分布（2D）保证均匀，再按 `angleJitter` 加扰动——纯随机会出现明显的疏密不均，纯均匀又太机械，`angleJitter` 是这两端之间的旋钮；② 长度：按 `lengthDistribution` 从 `lengthRange` 采样。`bimodal` = 20% 的针取上四分位长度、80% 取下半区，这直接产生"层次"；③ 每根：起点 = `origin + dir * originRadius`，终点 = 起点 + `dir * length`；根部宽 `widthRange` 采样，梢部宽 = 根部宽 × `(1 - taper)`；④ 四边形顶点 = 起点 ± 侧向 × 根宽/2、终点 ± 侧向 × 梢宽/2。侧向：`billboardMode` 时只写切线属性交给顶点着色器算（`cross(dir, viewDir)`），否则用 `cross(dir, coneAxis)`；⑤ `doubleQuad` 时第二片绕 `dir` 旋转 90°；⑥ `uv1.x` = 沿针的归一化长度（0=根，1=梢，供"从根向梢亮起"与"从梢向根消退"的阈值推进），`uv1.y` = 横向；`colors.b` = 针 id 归一化（逐根随机相位），`colors.r` = 该根的归一化长度（供"越长越亮"） |
| **服务层角色** | `flash / shock / edge / ground / core / emission`（作为几何而非粒子的放射） |
| **六档** | ML: `8 根，单片，billboard` (32 顶点) / MM: `12 根` (48) / MH: `32 根` (128) / PL: 同 MH / PM: `64 根，doubleQuad` (512) / PH: `96 根，doubleQuad` (768) |
| **2D-3D** | 3D：`coneAxis` 任意，`billboardMode` 默认 true；2D：方向限于 XY 平面等角分布，`billboardMode` 恒 false，`doubleQuad` 恒 false |
| **与材质版的分工** | 材质版 `sg_sdf_radial_burst`（`TECH_FAMILY_SPEC_MATERIAL.md` §3.2）在一张 quad 内解析绘制放射针，零几何成本但**不能被遮挡、不能有深度、长针在 quad 边界被裁**。几何版可以插进场景、可被前景遮挡、可以极长。判据：针长 > 宿主包围盒 1.5 倍或需要深度交互 → 用几何版；否则用材质版 |

### 4.20 `trail_mesh` — 尾迹（运行时组件，非生成器）

| 项 | 内容 |
|---|---|
| **载体** | `TrailRenderer` 组件（几何由 Unity 运行时生成） |
| **参数** | `time f`（尾迹时长）、`minVertexDistance f`、`widthCurve`、`widthMultiplier f`、`colorGradient`、`alignment i`（`View / TransformZ`）、`textureMode i`（`Stretch / Tile / DistributePerSegment` —— **一律不用 `Tile` 以外的重复贴图语义**，材质是程序化的）、`cornerVertices i`、`endCapVertices i`、`autodestruct false` |
| **要点** | ① `widthCurve` 必须非常数（ADR-009 MT-1 谓词的继承，见 §8）；② `colorGradient` 必须非单色（MT-2）；③ 池化复位时必须调 `Clear()`（现有 `GeneratedVfxController.ClearTrails` 的做法可参考）；④ 2D：`alignment = View` + `sortingOrder` 显式设置 |
| **服务层角色** | `trail / link` |
| **六档** | ML: `time=0.2, cornerVertices=0, minVertexDistance=0.2`（顶点少）/ MM~MH: `time=0.35, corner=1` / PL~PM: `time=0.5, corner=2, endCap=2` / PH: `time=0.8, corner=4, endCap=4` |

### 4.21 `line_mesh` — 线（运行时组件，非生成器）

| 项 | 内容 |
|---|---|
| **载体** | `LineRenderer` 组件 |
| **参数** | `positionCount i`、`widthCurve`、`colorGradient`、`useWorldSpace b`、`alignment i`、`loop b`、`cornerVertices` / `endCapVertices` |
| **要点** | 位置点由控制器逐帧写入（`link` 类原型的两端锚点、`chain` 类的节点序列）。**与 `jagged_polyline` 生成器的分工**：节点数在运行时变化（用户调 `addNode`）→ LineRenderer；节点数编译期固定且需要带宽/分叉/UV 拓扑 → 生成器网格 |
| **服务层角色** | `link / beam_column / edge` |

### 4.22 描边壳（outline shell，卡通 3D）

| 项 | 规格 |
|---|---|
| **实现** | **不生成第二份网格**：在同一 `MeshFilter` 上加第二个 `MeshRenderer`？Unity 不允许同 GameObject 两个 MeshRenderer。因此：层节点下加子节点 `Outline`，共享同一个 Mesh 资产（`MeshFilter.sharedMesh` 指向同一 asset，零额外内存），材质为 `SG_VfxLit` 的 `Cull Front` + `sg_vdisp_shell_extrude` 变体 |
| **成本** | +1 GameObject、+1 draw call、+0 网格内存 |
| **档位** | ML/MM 禁用（ADR-010 §4bis-5），改用材质 `sg_edge_dark_rim`（`TECH_FAMILY_SPEC_MATERIAL.md` §3.4）；MH+ 启用 |
| **层类别限制** | 只对 `inLayerKind == solid` 的层生成（`STYLE_CATALOG_v1.md` §2.7：透明体积层加描边会破坏体积感） |
| **2D** | 不生成描边壳，用材质 `sg_edge_outline_sdf` + `sortingOrder - 1` |

---

## 5. 顶点位移与材质族的分工边界

这是本文档与 `TECH_FAMILY_SPEC_MATERIAL.md` 之间最容易含糊的界面，因此给出**可判定的规则**而不是原则。

### 5.1 三个词的定义

| 词 | 归属 | 含义 |
|---|---|---|
| **几何变形（geometry deformation）** | 网格族 | 改变 Mesh 资产的顶点数据（编译期）或改变组件驱动的拓扑（TrailRenderer/LineRenderer/Cloth 运行时） |
| **顶点位移（vertex displacement）** | **材质族**（`sg_vdisp_*`，在顶点着色器内） | 不改 Mesh 资产，每帧在 GPU 上偏移顶点位置 |
| **像素形态（pixel-level form）** | 材质族（`sg_noise_*` / `sg_sdf_*` + 阈值） | 完全不动顶点，靠 alpha 与颜色决定"看起来是什么形状" |

### 5.2 判定规则（按序，命中即止）

| # | 条件 | 归属 |
|---|---|---|
| R1 | 形态需要被**物理引擎**作用（碰撞、约束、刚体） | **网格族**（预破碎 / Cloth） |
| R2 | 形态的**拓扑在运行时变化**（节点增删、尾迹增长） | **网格族**（TrailRenderer / LineRenderer + 控制器） |
| R3 | 形态需要**自遮挡或被前景遮挡**，且轮廓必须精确（不能靠 alpha 裁剪近似） | **网格族**（真几何） |
| R4 | 形态的**轮廓每帧变化**且变化幅度 > 该层包围盒的 10% | **网格族生成足够顶点 + 材质族顶点位移**（两族协作：网格给顶点密度，材质给每帧偏移） |
| R5 | 形态的轮廓变化幅度 ≤ 10%，或只是"表面起伏" | **材质族顶点位移**（`sg_vdisp_*`），网格用低细分基底 |
| R6 | 形态可以用 alpha 完全表达（软边、雾、光晕、贴地阵纹、UI） | **材质族像素形态**，网格用单 quad |
| R7 | 其余 | **材质族像素形态**（默认更省） |

### 5.3 规则的推论（供实现者直接照用）

| 层角色 | 典型归属 | 说明 |
|---|---|---|
| `core`（发光核） | R6 → 材质（quad + SDF）；元素为晶体/岩石类时 R3 → 网格 | 同一层角色在不同元素下归属不同，这正是元素轴的作用 |
| `body`（体积） | R6 → 材质 | 永远不给体积层生成真几何（成本无收益） |
| `trail` | R2 → 网格（TrailRenderer） | |
| `link` | 节点固定 → 生成器；节点动态 → LineRenderer | |
| `mesh_shell` | R3 → 网格 + R5 材质位移 | 壳必须是真几何（要被遮挡），起伏靠材质位移 |
| `debris` | R1 → 网格（预破碎） | |
| `surface` | R4 → 网格（细分平面）+ 材质 Gerstner | 波幅通常 > 10% |
| `cloth` | R1 → 网格（Cloth）；低档 R5 → 材质位移 | 档位切换会改变归属，两条路的参数面必须对齐（同 `windVector` / `stiffness` 语义） |
| `ground / decal` | R6 → 材质（贴地 quad） | |
| `shock` | 2D R6 → 材质极坐标环；3D 需要贴合地形起伏时 R3 → `ring_torus_segments` | |
| `column / beam_column` | R3 + R5 → 网格柱 + 材质弯曲 | |
| `veil` | R6 → 材质（大 quad）；需要飘动褶皱时 R4 → 细分平面 + 材质波 | |
| `flash` | R6 → 材质 | 唯一例外：需要极长放射针且要被遮挡时用 `radial_spike_array`（§4.19） |
| `orbit` | R3 → 网格（几何符号/框线体） | |

### 5.4 像素风的几何量化归属

`sg_vdisp_grid_snap`（材质族）与生成器的顶点量化是**两件事**：

- **材质族 `sg_vdisp_grid_snap`**：每帧在 GPU 上把顶点吸附到栅格。**默认路径**，因为物体旋转/移动时栅格跟随的规则由 `snapSpace` 决定，运行时才知道。
- **生成器的顶点量化**（每个生成器的可选参数 `quantizeVertices`）：编译期把生成的顶点吸附到栅格。用于**静态几何**（不旋转的 `ground / veil / surface`），可以省掉每帧的量化指令，并且使碰撞体也是量化的。
- 二者**不得同时启用**（谓词 MS-8）。

---

## 6. 局部光族：局部光节拍器组件

### 6.0 定位

ADR-010 §5：**局部光是原型作曲的一等成员，不是可选装饰**。`PROTOTYPE_CATALOG_v1.md` §1.2：凡有发光语义的原型 `light` 层为必需，低档策略是"把光烘进材质发光"而不是删掉这个职责。58 原型中 `light` 出现 98 处，是必需层最多的角色。

因此本族的实现是**一个统一组件**（`VfxLightBeat`），而不是"给 Light 组件调参数"。组件承担：节拍计算、风格量化、双驱动、`光:烘` 退化、与辉光层的同步。

### 6.1 组件形状

```
Layers/<layerId>                    GameObject
├─ VfxLightBeat（MonoBehaviour，本节规格）
├─ Light（3D，Point 或 Spot）        —— 或 ——
├─ Light2D（2D，Point / Freeform / Sprite）
└─ （光:烘 档：以上两者都不存在，组件仍在）
```

### 6.2 参数面

以 `T2A_REPORT.md` §5.1-4 为起点，补齐风格量化、双驱动、耦合与退化所需的字段。

| 参数 | 类型 | 范围 | 来源 | 语义 |
|---|---|---|---|---|
| `color` | Color | — | 元素预设（色温或直接色） | 光色。元素给色温时编译期转 RGB（黑体辐射近似），非黑体元素直接给色 |
| `intensity` | float | 0~20 | 元素 `intensityMul` × 标准参数 `intensity` | 基础强度（3D 单位与 2D 单位不同，见 §6.4） |
| `range` | float | 0.1~50 | recipe / 原型 | 3D `Light.range`；2D `Light2D.pointLightOuterRadius` |
| `innerAngle` / `outerAngle` | float | 0~180 | recipe | Spot（3D）/ Light2D 的角度；Point 时忽略 |
| `flickerMode` | enum | `steady / breathe / flicker / pulse / strobe` | 元素预设 | 见 §6.3 |
| `flickerRate` | float | 0.1~30 | 元素预设 | Hz |
| `flickerDepth` | float | 0~1 | 元素预设 | 调制深度（0=不变，1=在 0 与峰值间摆动） |
| `decayShape` | enum | `exp / linear / step / smooth / spike` | 元素预设 | 阶段收尾时的强度衰减形状 |
| `castShadows` | bool | — | 风格约束 × 档位 | 3D 有效；2D 的 `Light2D.shadowsEnabled` |
| `intensitySteps` | int | 0~8 | 风格约束 | 0=连续；>0 时强度量化到 N 档（卡通 3、像素 4） |
| `flickerQuantize` | bool | — | 风格约束 | 把连续闪烁曲线转为阶跃 |
| `beatFrameRate` | float | 0~30 | 风格约束 | 节拍的时间量化（像素风 12） |
| `saturationMul` | float | 0.5~2 | 风格约束 | 光色饱和度倍率 |
| `bakeMode` | enum | `auto / light / material` | 档位 | `auto`（默认）按档位决定；`material` 强制烘进材质发光 |
| `bakedEmissionMul` | float | 0~4 | 元素/档位 | `光:烘` 时写入材质的发光倍率基准（§6.6） |
| `followTarget` | enum | `self / latestNode / hostCenter` | 原型 | 多节点原型中光跟谁（如"跟随最新节点"） |
| `beatChannel` | int | 0~3 | 原型 | 同一预制体内可有多条独立节拍（多盏灯不同相位）；材质通过 `_BeatValue0…3` 接收 |
| `phaseOffset` | float | 0~1 | recipe | 同 channel 内的相位偏移（多盏灯错开） |

### 6.3 五种 `flickerMode` 的波形定义

输入 `t = quantizedTime * flickerRate`（`quantizedTime` 见 §6.5），输出 `b ∈ [0,1]`，最终 `beat = 1 - flickerDepth * (1 - b)`。

| mode | 波形 | 观感 |
|---|---|---|
| `steady` | `b = 1` | 不变 |
| `breathe` | `b = 0.5 + 0.5 * sin(2π t)` | 平滑呼吸 |
| `flicker` | `b = fbm1d(t)`（两层 value noise，频率比 1:2.7） | 不规则抖动（燃烧类） |
| `pulse` | `b = pow(frac(t), 0.25)` 的反向 —— 即 `b = 1 - pow(frac(t), 0.25)`：瞬亮后拖尾衰减 | 尖峰脉冲 |
| `strobe` | `b = step(frac(t), dutyCycle)`，`dutyCycle` 默认 0.35 | 频闪（硬开关） |

`flickerQuantize = true` 时：`b = floor(b * intensitySteps) / max(intensitySteps - 1, 1)`。这把 `breathe` 的正弦变成阶梯呼吸、把 `flicker` 的噪声变成跳档，符合卡通/像素风的读感要求。

### 6.4 3D `Light` / 2D `Light2D` 双驱动

组件不持有对 `Light` 或 `Light2D` 的强类型引用（避免 2D 包依赖污染 3D 产物）。实现方式：

```
[SerializeField] private Component lightTarget;   // Light 或 Light2D，编译期赋值
[SerializeField] private LightDriverKind kind;    // None | Light3D | Light2D | MaterialOnly
```

`kind` 是编译期决定的枚举，运行时按 `kind` 走三条互斥分支（无反射、无 `is` 链，与 `VfxBindingKeys` 的"显式键→处理器，禁反射"同构）：

| kind | 写入目标 | 强度换算 |
|---|---|---|
| `Light3D` | `Light.intensity = intensity * beat`；`Light.color`、`range`、`spotAngle`、`shadows` | 3D URP 的 intensity 单位（默认 Lumen 模式下需换算，编译期按项目的 `UniversalRenderPipelineAsset` 光照单位设置烘一个 `unitScale` 常量到组件字段——**运行时不查询管线设置**） |
| `Light2D` | `Light2D.intensity = intensity * beat * unitScale2D`；`color`、`pointLightOuterRadius`、`pointLightInnerRadius`、`shadowsEnabled` | 2D 的 intensity 语义与 3D 不同，`unitScale2D` 默认 1.0，元素预设给 2D 单独的 `intensityMul2D` |
| `MaterialOnly` | 不写任何 Light，只写材质（§6.6） | — |
| `None` | 组件禁用（不应出现在产物中，谓词 LT-1） | — |

**为什么用 `Component` + 枚举而不是两个字段**：两个强类型字段会让 3D 产物的序列化数据里出现 `Light2D` 类型引用，进而把 URP 2D 的程序集变成 3D 预制体的依赖。`Component` + 编译期枚举避免这一点。

### 6.5 时间与相位所有权

`VfxLightBeat` 是**该 beatChannel 的相位唯一所有者**：

```
LateUpdate():
   localTime  = controller.PhaseTime * speed          // 控制器给的层局部时间
   qTime      = beatFrameRate > 0 ? floor(localTime * beatFrameRate) / beatFrameRate : localTime
   b          = Waveform(qTime * flickerRate + phaseOffset)   // §6.3
   if flickerQuantize: b = Quantize(b, intensitySteps)
   beat       = 1 - flickerDepth * (1 - b)
   beat      *= DecayEnvelope(controller.PhaseProgress, decayShape)
   ApplyToLight(beat)                                  // §6.4
   BroadcastBeat(beat)                                 // §6.7
```

用 `LateUpdate` 而不是 `Update`：确保控制器已在 `Update` 里推进过阶段时间，同一帧内光与材质读到的是同一个阶段状态。

### 6.6 `光:烘` 档退化的具体机制

`PROTOTYPE_CATALOG_v1.md` §2.4 的 ML 档："局部光数 / 预制体 = 0（烘进材质发光）"。降级表记法 `光:烘`。

**编译期动作**：

1. **不生成** `Light` / `Light2D` 组件；`VfxLightBeat` 仍然生成，`kind = MaterialOnly`。
2. 确定"被这盏光照亮的层集合"：编译器取该 `light` 层在原型层表中的**耦合层**（原型目录为每个 `light` 层声明 `illuminates: [layerId...]`；未声明时默认为同一预制体内除 `veil` 外的全部材质族层）。
3. 为每个被照层的材质增加一个烘焙发光项：`emission += bakedEmissionMul * lightColor * beat * falloffApprox`，其中 `falloffApprox` 是**编译期计算的常数**——按该层节点与光节点的初始距离和 `range` 算出的一个衰减系数（`saturate(1 - dist/range)^2`），烘进材质属性 `_BakedLightFalloff`。
4. 运行时 `VfxLightBeat` 的 `MaterialOnly` 分支每帧把 `beat` 写入这些材质的 `_BakedLightBeat`（通过 `MaterialPropertyBlock`）。

**为什么这不是"把光删了"**：光的三个信息——颜色、强度节拍、空间衰减——全部保留了两个（颜色与节拍是精确的，衰减是编译期近似的静态值）。丢掉的只有"随相对位置变化的衰减"。对一个自包含特效来说，各层的相对位置在预制体内本就基本固定，这个近似的误差很小。**丢掉的另一件事是照亮用户场景**——这在 ML 档是可接受的取舍，且必须在编译报告中登记。

**谓词 LT-4** 断言：`光:烘` 档产物中 `Light`/`Light2D` 组件数为 0，且 `VfxLightBeat.kind == MaterialOnly`，且至少一个材质有非零 `_BakedLightFalloff`——**不允许"降级 = 什么都没有"**。

### 6.7 与辉光层 `flickerCoupling` 的同步方式

`TECH_FAMILY_SPEC_MATERIAL.md` §5.8 要求辉光层与光同呼吸。同步机制：

1. `VfxLightBeat` 在 `LateUpdate` 末尾调 `BroadcastBeat(beat)`。
2. 广播目标是编译期烘死的一个**目标数组**：`Renderer[] beatTargets`（该 channel 的全部订阅渲染器，含被照层的主渲染器与它们的 `Glow_k` 子节点渲染器）。
3. 用一个共享的 `MaterialPropertyBlock` 写 `_BeatValue{channel}`，逐个 `renderer.SetPropertyBlock(mpb)`。**不用 `Shader.SetGlobalFloat`**：全局属性会泄漏到用户场景中的其他材质，违反"对外部零假设"。
4. 材质侧：`glowIntensity = base * lerp(1, _BeatValue{channel}, flickerCoupling)`。`flickerCoupling = 0` 时光晕不受节拍影响（但仍读同一个值，无分支）。

**多 channel**：一个预制体最多 4 条 channel（`_BeatValue0…3`）。多盏灯若共用 channel 则同相（可用 `phaseOffset` 错开），不同 channel 则完全独立。谓词 LT-5 断言每个 `VfxLightBeat` 的 `beatTargets` 非空且全部在同一预制体内。

### 6.8 元素 → 光预设速查表

| 元素 | color / 色温 | `intensity` 倍率 | `flickerMode` / `rate` / `depth` | `decayShape` | 备注 |
|---|---|---|---|---|---|
| `fire` | 1800~2400 K（橙） | 1.3 | `flicker` / 8~14 Hz / 0.25 | `exp` | |
| `ice` | 8000~12000 K（冷白蓝） | 0.7 | `steady`（或 `breathe` 0.3 Hz / 0.1） | `linear` | |
| `lightning` | 9000~15000 K（白蓝紫） | 2.0 | `strobe` / 15~30 Hz / 0.8 | `step` | 频闪是本元素的身份 |
| `water` | — | 0.2 | `steady` | `smooth` | **默认 `enabled = false`**，除非显式开 |
| `wind` | — | — | — | — | **默认关** |
| `earth` | — | — | — | — | **默认关** |
| `poison` | 直接色 `primary`（黄绿） | 0.6 | `breathe` / 0.6 Hz / 0.3 | `smooth` | 非黑体，不用色温 |
| `light` | 4500~5500 K（暖白） | 1.5 | `breathe` / 0.5 Hz / 0.15 或 `steady` | `smooth`（长尾） | |
| `shadow` | 直接色 `hot`（紫） | 0.3 | `flicker` / 2~4 Hz / 0.4 | `smooth`（拖尾） | Unity 光不能为负；"变暗"由材质 `sg_comp_dark_core` 承担，光只给极弱紫边 |
| `arcane` | 直接色 `primary`（紫蓝） | 1.0 | `pulse` / 0.7 Hz / 0.5 | `spike` | |
| `tech` | 直接色 `primary`（青）/ `secondary`（橙） | 1.0 | `pulse` / 1.5 Hz / 0.4（方波）或低概率 `strobe` | `step` | |
| `blood` | — | — | — | — | **默认关** |
| `nature` | 直接色 `hot`（黄绿孢子光） | 0.5 | `breathe` / 0.4 Hz / 0.2 | `smooth` | |
| `none.*` | — | — | — | — | 全部子画像 `light` 关（`ELEMENT_CATALOG_v1.md` §4） |

### 6.9 风格 × 局部光交互

| 风格约束 | 卡通 | 像素 |
|---|---|---|
| `intensitySteps` | 3 | 4 |
| `flickerQuantize` | true | true |
| `beatFrameRate` | 0（不做时间量化） | 12 |
| `saturationMul` | 1.2 | 1.1 |
| `castShadows` | 允许（PM/PH，建议硬阴影） | **不允许**（软阴影与像素风冲突，`STYLE_CATALOG_v1.md` §3.5） |
| `range` 量化 | 不量化 | 建议量化到 `pixelWorldSize` 的整数倍（`STYLE_CATALOG_v1.md` §3.5 承认 falloff 无法量化，接受连续光） |

### 6.10 局部光的档位表

| 档 | 光数 / 预制体 | 阴影 | 说明 |
|---|---|---|---|
| ML | 0（`光:烘`） | — | §6.6 |
| MM | 1 | 无 | 多盏时保留 `priority` 最高的一盏（原型层表声明），其余转 `MaterialOnly` |
| MH | 2 | 无 | |
| PL | 2 | 无 | |
| PM | 3 | ≤ 1 盏 | |
| PH | 4 | ≤ 2 盏 | |

超出档位光数时的降级顺序：按原型层表的 `light` 层 `priority`，低优先级的转 `MaterialOnly`（不是删除）。这保证任何档位下"发光职责"都在。

---

## 7. 元素 → 生成器速查表

| 元素 | 主生成器 | 关键参数取向 | 破碎风格 | `link` 形态 | `column` 形态 |
|---|---|---|---|---|---|
| `fire` | `sweep_band`（多层）/ `cylinder_beam` | `layerCount=2~3`，`bulgeProfile` 上宽下窄 | 烧穿（材质阈值，无块） | `catenary_band`（振幅小频率高） | 上宽下窄锥柱 |
| `ice` | `crystal_cluster` | `sides=6`，`tipRatio=0.1`，`alignment=0.6` | `voronoi_prefracture`（块状直落） | `jagged_polyline`（硬拐点，`jitter` 中） | 由地面向上的棱柱簇 |
| `lightning` | `jagged_polyline` | `rephaseRate` 高，`branchDepth=2~3` | 电离散裂（材质，无块） | 分叉折线（主场） | 锯齿柱 |
| `water` | `parabola_tube` / `splash_crown`(§4.23) / `subdivided_plane` | 截面随流速收窄 | 化水（材质阈值 + 顶点下垂） | 抛物管 | 水柱 |
| `wind` | `spiral_ribbon` | `turns=3~5`，`twist` 大 | 风散（材质 + 切向飘移） | 松弛波动带 | 涡旋锥 |
| `earth` | `rock_chunk` / `voronoi_prefracture` | `roughness` 高，`flatnessBias` 中 | **块状 Voronoi + Rigidbody（主场）** | — | 隆起岩柱 / 块堆 |
| `poison` | `tendril_blob` | `curl` 大，`tendrilThickness` 粗 | 溶解（材质 + 顶点下垂） | 下垂粘稠带（`catenary_band` 高 `sag`） | 上涌泡柱 |
| `light` | `cylinder_beam` / `ring_torus_segments` / `radial_spike_array` | 干净几何，`angleJitter` 低 | 光化（材质，无块） | 直线光带（无抖动） | 干净圆柱 |
| `shadow` | `tendril_blob` | `tendrilCount` 多，`curl` 大 | 吞噬（材质阈值从边缘向中心） | 抽搐黑带 | 向下压的黑柱 |
| `arcane` | `wire_polyhedron` / `ring_torus_segments` | 多环反向旋转，`nodeStyle=sphere` | 相位散解（碎成小多面体旋转飞散） | 直线 + 节点符号 | 符文柱 |
| `tech` | `tech_panel` / `cylinder_beam(hexagon)` | `gridType=hex`，`missingRatio` 小 | 栅格化脱落（逐格，`colors.b` 驱动） | 直角折线（`manhattan`） | 六边柱 + 扫描环 |
| `blood` | `splash_crown` / `parabola_tube` | 冠柱数随机、高度随机 | 不适用（血不做壳） | 抛物管（脉冲截面） | — |
| `nature` | `vine_spline` / `branch_tree` | `leafCount` 高，`coilAround=true` | 枯萎（材质 + 顶点下垂 + 色变） | 缠绕藤蔓（主场） | 生长藤柱 |
| `none.rubble` | `rock_chunk` + `voronoi_prefracture` | 中性 | 块状 Voronoi | — | — |
| `none.cloth` | `cloth_patch` | `pinMode=topEdge` | 撕裂 = 约束断开或阈值挖空 | — | — |
| `none.liquid` | `parabola_tube` / `splash_crown` | 同 water 但无色 | — | 抛物管 | — |

### 4.23 `splash_crown` — 飞溅冠（补齐 §3 分类中的"面/板"类第三项）

| 项 | 内容 |
|---|---|
| **输入参数** | `crownRadius f`、`spikeCount i 4~24`、`spikeHeight f2`（随机范围）、`spikeWidth f`、`rimHeight f`、`irregularity f 0~1`、`sheetSegments i`（冠壁的环向细分） |
| **顶点数量级** | `sheetSegments * 2 + spikeCount * 4`；24 × 2 + 12 × 4 = 96 |
| **生成算法要点** | ① 底部圆环 + 向上外扩的薄壁（冠形）；② 沿环按 `spikeCount` 长出细柱，高度在 `spikeHeight` 内随机，`irregularity` 控制柱位置与高度的随机度（0=完全规则，参考图里的液体冠总是不规则的）；③ 柱顶可选加一个小球（将断未断的液滴）；④ `uv1.y` = 沿高度（供"冠向上生长再回落"的阈值推进） |
| **服务层角色** | `core.crown / shock / ground` |
| **六档** | ML: `8 段 + 4 柱` (32) / MM: `12+6` (48) / MH: `24+12` (96) / PL: 同 / PM: `36+18` (144) / PH: `48+24` (192) |
| **2D-3D** | 2D：冠退化为一条起伏的轮廓带 + 若干竖直条 |

---

## 8. 构造性谓词（EditMode 机器检查）

### 8.0 可断言性设计：生成器清单与网格 sidecar

```
Assets/VFX/Shared/MeshGenerators/<id>.generator.json
   { "id", "version", "parameters": [{name, type, min, max, default}],
     "outputsUv1": true, "outputsColors": true, "dimensions": ["2d","3d"],
     "vertexBudgetByTier": {"ML":…,"PH":…}, "producesCollider": bool, "producesRigidbody": bool }

Assets/VFX/Generated/<recipeId>/Meshes/<layerId>.asset          （网格资产）
Assets/VFX/Generated/<recipeId>/Meshes/<layerId>.mesh.json      （生成溯源 sidecar）
   { "generatorId", "generatorVersion", "params": {...}, "seed", "dimension",
     "vertexCount", "triangleCount", "bounds", "sha256" }
```

网格 sidecar 使"这个网格是由哪个生成器用什么参数产出的"可机器验证，且支持**重生成一致性检查**（谓词 MS-2）。

### 8.1 网格族谓词（MS-*）

| 编号 | 谓词 |
|---|---|
| MS-1 | `Assets/VFX/Shared/MeshGenerators/` 下每个生成器清单的 `id` 匹配 `^[a-z][a-z0-9_]*$` 且唯一；清单集合恰等于 §3 总览表的 20 个生成器（**未立法生成器不得存在**） |
| MS-2 | 产物目录内每个 `.asset` 网格必有 `.mesh.json` sidecar；用 sidecar 记录的 `(generatorId, params, seed, dimension)` **重新生成一次**，断言 `vertexCount / triangleCount / bounds / sha256` 与 sidecar 完全一致。这是确定性（`RECIPE_V2_SCHEMA_DRAFT.md` §1-6）的机器证明 |
| MS-3 | 每个生成的网格必须有非空 `normals`、`uv`（uv0）、`uv2`（uv1 通道）、`colors`（§1.4 契约）。缺任一项 FAIL |
| MS-4 | 每个网格的 `vertexCount` ≤ 该档位的顶点上限（§2.4 的"程序化网格顶点/层"）；一个层含多个网格（如预破碎的碎块）时按层求和 |
| MS-5 | 每个网格的 `bounds` 有限、非零体积（退化网格 = 生成器 bug） |
| MS-6 | `MeshFilter.sharedMesh` 必须是产物目录内的 `.asset` 或 Unity 内置基础几何（Cube/Sphere/Quad/Plane/Cylinder/Capsule）。**任何其他网格资产一律拒绝**（零外部模型导入，ADR-010 §5） |
| MS-7 | `MeshRenderer.sharedMaterial` 的 shader 必须是材质族 4 张主图之一（`TECH_FAMILY_SPEC_MATERIAL.md` MV-6 的网格侧延伸） |
| MS-8 | 同一层不得同时启用生成器顶点量化（sidecar 的 `params.quantizeVertices == true`）与材质 `sg_vdisp_grid_snap`（§5.4） |
| MS-9 | 描边壳子节点（`Outline`）的 `MeshFilter.sharedMesh` 必须与宿主指向**同一个** Mesh 资产（GUID 相同，§4.22 的零内存要求）；其材质必须是 `Cull Front` 的 `SG_VfxLit` 变体 |
| MS-10 | ML/MM 档产物中不存在 `Outline` 子节点（ADR-010 §4bis-5） |
| MS-11 | `TrailRenderer`：`widthCurve` 键数 ≥ 2 且存在两键值不同；`colorGradient` 非单色（色差或 alpha 差）。**这是 ADR-009 MT-1 / MT-2 谓词的直接继承** |
| MS-12 | `LineRenderer`：同 MS-11 的宽度与颜色断言；`positionCount ≥ 2` |

### 8.2 物理谓词（PH-*）

| 编号 | 谓词 |
|---|---|
| PH-1 | 预破碎层的碎块子节点数 == sidecar 的 `fragmentCount`，且 == 该档位的破碎块上限内 |
| PH-2 | 每个碎块的 `Rigidbody.isKinematic == true` 且 `useGravity == false`（初始状态；§4.17b） |
| PH-3 | ML 档产物中破碎层的 `Rigidbody` 组件数 == 0（§4.17b 的 ML 行） |
| PH-4 | 碎块的 `Collider` 类型 ∈ {BoxCollider, MeshCollider(convex=true), PolygonCollider2D}；`MeshCollider.convex` 为 false 的一律 FAIL（非凸 MeshCollider 不能做动态刚体） |
| PH-5 | 碎块不改 GameObject 的 `layer`（== 预制体根的 layer，§4.17b） |
| PH-6 | `Cloth` 组件：`coefficients` 数组长度 == 网格顶点数；至少一个系数的 `maxDistance == 0`（有固定点，否则布料会掉下去） |
| PH-7 | ML/MM 档产物中 `Cloth` 组件数 == 0（§4.18 档位表） |
| PH-8 | `Cloth.capsuleColliders` / `sphereColliders` 在产物中为空数组（对外部零假设，§4.18） |
| PH-9 | 产物中零个 `Rigidbody` 的 `constraints` 引用外部对象；零个 `Joint` 组件（v1 不用关节） |

### 8.3 局部光族谓词（LT-*）

| 编号 | 谓词 |
|---|---|
| LT-1 | 每个 `light` 角色的层节点上恰好一个 `VfxLightBeat`，且 `kind != None` |
| LT-2 | `kind == Light3D` 时 `lightTarget` 是 `Light` 且 `type ∈ {Point, Spot}`（**禁止 Directional** —— 那是全局光，ADR-010 §4 资产外）；`kind == Light2D` 时 `lightTarget` 是 `Light2D` 且 `lightType ∈ {Point, Freeform, Sprite}`（禁止 `Global`） |
| LT-3 | 产物中 `Light` + `Light2D` 组件总数 ≤ 该档位的"局部光数/预制体"上限；带阴影的盏数 ≤ 档位上限 |
| LT-4 | `光:烘` 档：`Light`/`Light2D` 组件数 == 0 **且** 至少一个 `VfxLightBeat.kind == MaterialOnly` **且** 至少一个材质的 `_BakedLightFalloff != 0`（§6.6：降级不等于什么都没有） |
| LT-5 | 每个 `VfxLightBeat.beatTargets` 非空，且每个目标都在同一预制体的层次内（不引用外部对象） |
| LT-6 | `flickerMode != steady` 的组件，其 `flickerDepth > 0`（否则声明了闪烁却不闪，是配置错误）。**这是 ADR-009 "静态即失败"方法论在光族的对应物** |
| LT-7 | 风格为 `pixel` 时全部 `castShadows == false`（`STYLE_CATALOG_v1.md` §3.5） |
| LT-8 | `VfxLightBeat` 不调用任何 `Shader.SetGlobal*`（§6.7）。断言方式：组件源码经一条源码级测试扫描（与 `VfxOutputAuditor` 扫描禁止组件类型同构），或组件清单声明 `usesGlobalShaderProperties: false` 并由源码 grep 测试守护 |
| LT-9 | 一个预制体内 `beatChannel` 的取值 ⊆ {0,1,2,3}，且每个被使用的 channel 恰有一个"所有者"组件（同 channel 多个所有者会互相覆盖） |

### 8.4 fail-closed 三路（本两族具体化）

1. **未立法生成器拒绝**：生成器清单外的 id → MS-1 FAIL；`technique.variant` 引用不存在的生成器 → `E203`。
2. **无变体声明拒绝**：网格族变体清单未声明该 `(generator, params)` 组合 → `E203`。
3. **资产不在白名单拒绝**：`MeshFilter.sharedMesh` 不是产物网格或内置几何 → MS-6 FAIL；网格材质 shader 不是主图之一 → MS-7 FAIL。

显式豁免机制与材质族同构。

---

## 9. 审计清单

- [ ] 20 个生成器 + 2 个运行时几何组件全部有 id / 输入参数 / 顶点数量级 / 生成算法要点 / 服务层角色 / 六档顶点预算 / 2D-3D 差异。
- [ ] `radial_spike_array` 已补入（ADR-010 §4bis-8），含长度/密度/随机角/锥度可调与 `bimodal` 长度分布。
- [ ] 预破碎与 Rigidbody 接线（层次 / 初始状态 / 触发 / layer 纪律 / 休眠回收 / ML 无刚体路径 / 2D）。
- [ ] Cloth 六档规格 + 组件接线 + 对外部零假设的碰撞体策略。
- [ ] 顶点位移与材质族的分工边界：3 个词的定义 + 7 条判定规则 + 层角色推论表 + 像素量化归属。
- [ ] 局部光节拍器：参数面 20 项 / 5 种波形 / 双驱动（无反射的 kind 分支）/ `光:烘` 四步机制 / `flickerCoupling` 同步（MPB 广播、禁全局属性）/ 元素速查 / 风格交互 / 档位表。
- [ ] 构造性谓词 30 条（MS 12 / PH 9 / LT 9）+ fail-closed 三路。
- [ ] 无外部模型导入（MS-6）；无 Directional Light（LT-2）；无场景力场 / 风区；无序列帧。
- [ ] 正文无具体特效名。
