# 技术族变体库 v1（Technique Variant Library v1）

状态：`ACTIVE`（T2b 产出，2026-09-05）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）
配套：`PROTOTYPE_CATALOG_v1.md`（§2.4 六档基准预算）、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`recipe-v2.schema.json`（本清单的变体 id 即 schema 各族 `variant` 闭集枚举）、`docs/design/references/REFERENCE_ANALYSIS.md`（§3bis 光晕参数面）

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **技术族轴** | **5 族全部**：材质 16 变体 / GPU 粒子 10 场模板 / CPU 粒子 11 模块谱 / 网格 19 生成器（含 `radial_spike_array`，ADR-010 §4bis-8）/ 局部光 1 节拍器。每变体给出 id / 2D-3D 支持 / 参数面 / 六档成本估算 / 构造性门禁谓词 |
| **原型轴 / 元素轴 / 风格轴** | 不新增、不修改；变体是原型层的实现选项，元素预设按 (角色, 族) 选变体并给参数默认，风格作为材质末端 `StyleStage` 与约束参数集叠加 |
| **维度轴** | 每变体标注 2D / 3D 支持与差异 |

**纪律**：本文档无具体特效名；变体按"能算出什么形态 / 能驱动什么运动"命名。

## 1. 变体、子图、资产三者的关系

- **变体（variant）**：recipe `layers[].technique.variant` 的取值单位，每族一张闭集表（即 `recipe-v2.schema.json` 的 `materialVariant / gpuParticlesVariant / cpuParticlesVariant / meshVariant / localLightVariant` 枚举）。变体 = 参数面 + 门禁谓词 + 成本函数的绑定单元。
- **子图（subgraph）**：Shader Graph 函数级资产（`project/Assets/VFX/TechniqueFamilies/ShaderGraph/Subgraphs/`），是材质变体的内部构件；多个变体可复用同一子图（如 `polar_uv`、`StyleStage` 插槽）。子图不直接出现在 recipe 中。
- **骨架资产（skeleton asset）**：每个材质变体一个 `.shadergraph` 主图（挂 `StyleStage` 插槽）；每个网格变体一个 C# 生成器；每个 CPU 粒子变体一个模块谱应用器；GPU 粒子变体走「模板描述 + Editor 脚本化构建」路线（§4.1）。

**编译期合并顺序**（不变，见元素目录 §2.1）：原型层默认 ← 元素预设 (role, family) ← 风格约束 ← recipe `parameters` ← `tierOverrides` ← 档位截断。

## 2. 门禁谓词通则（继承 ADR-009 方法论）

全部谓词是**构造性、机器可查、fail-closed** 的：EditMode 测试构造性遍历资产/生成器输出，断言下表"门禁谓词"列；不满足即红，豁免必须进显式豁免表并登记到期卡。通用谓词（对全部族生效）：

| # | 谓词 | 查法 |
|---|---|---|
| G-1 | 零序列帧：任何变体资产不得引用 flipbook/atlas 采样节点或序列帧贴图 | 资产文本扫描（Shader Graph JSON 节点类型 / ParticleSystem TextureSheetAnimation 模块 off） |
| G-2 | 自包含：变体资产依赖闭包只落在准入白名单目录内 | `AssetDatabase.GetDependencies` 前缀断言 |
| G-3 | 确定性：同 seed 同参数两次生成，网格顶点/材质属性字节相同 | 生成两次做逐元素比较 |
| G-4 | 资产外零触碰：变体产物组件集 ⊆ 编译器识别组件闭集，无相机/Volume/全局光/timeScale 引用 | 组件类型白名单断言 |

## 3. 材质族（Shader Graph）——16 变体

主图统一约定：输入 = UV/位置 + 标准参数（palette 5 色、intensity、speed、seed）+ 变体参数；末端固定 `StyleStage` 子图插槽（`Style_Cartoon / Style_Pixel / Style_None` 三实现，风格由 `style.id` 决定挂哪个）；输出 = 最终颜色 + alpha。全部形态程序化（噪声/SDF/阈值/顶点位移），无任何贴图采样（LUT/遮罩类外部辅助贴图仅允许作为可选输入端口，默认不接）。

成本记法沿用原型目录 §2.4：`材n` = 噪声采样层数 n；顶点位移仅 PH 档默认开。

| 变体 id | 2D/3D | 参数面（标准参数外） | 成本估算（六档） | 门禁谓词 |
|---|---|---|---|---|
| `noise_fbm_2layer` | both | `scrollDir(vec2)`、`speedRatio`(1~4，双层速比)、`stretch`(各向异性拉伸 1~3)、`detailWeight`、`thresholdWidth` | ML 材1（单层静采）→ MM/PL 材2 → PM 材2+细节 → PH 材3 | M-1：≥1 个 Time 驱动输入（静态豁免标记 `static` 除外）；M-2：双层速比参数存在且默认 ≠1（各向异性可查：`stretch` 端口存在） |
| `noise_voronoi_crystal` | both | `cellDensity`、`facetContrast`、`interiorScatter`(内散射权重)、`growthProgress`(0~1 结晶生长) | 同上 | M-1；M-3：`growthProgress` 属性存在（结晶生长可绑定） |
| `noise_jagged_1d` | both | `jitter`、`rephaseRate`(跳变 Hz，floor 采样)、`forkWeight`(分叉通道权重)、`coreWidth`、`haloWidth`(双阈值) | ML 材1 → 其余 材2（1D 噪声便宜） | M-1；M-4：时间采样必须经 floor 量化（跳变而非插值，节点链可查）；M-5：双阈值两参数并存 |
| `sdf_shape` | both | `shape(circle/ring/star/polygon/fan/roundrect/leaf/cross)`、`sides`、`innerRadius`、`softness`、`rotation` | 全档 材1（纯解析 SDF） | M-6：形状枚举全部由解析 SDF 构成（无纹理）；G-1 |
| `sdf_sacred_pattern` | both | `rings`、`rays`、`polygonSides`、`patternSeed`、`lineWidth` | 材1~2 | M-6 |
| `sdf_rune_ring` | both | `ringCount`、`tickCount`、`glyphSeed`、`counterRotate`(层反向转)、`pulsePeriod` | 材1~2 | M-6；M-1 |
| `sdf_hex_grid` | both | `cellSize`、`litRatio`(逐格点亮比)、`edgeWidth`、`stepRate` | 材1 | M-6；M-4（逐格点亮走阶跃时间） |
| `sdf_scanline` | both | `lineDensity`、`scanSpeed`、`duty`、`glitchChance` | 材1 | M-6；M-1 |
| `sdf_crack_branch` | both | `branchDepth`(1~4)、`branchAngle`、`crackWidth`、`progress`(0~1 蔓延)、`seedPoint(vec2)` | 材1~2 | M-6；M-3（progress 可绑定） |
| `threshold_dissolve` | both | `mode(soft/hard/grow)`、`thresholdWidth`、`progress`、`growFrom(point/edge/voronoiSeed)`、`edgeGlowWidth` | 材1（叠加于宿主噪声上） | M-3；M-7：`mode` 三型齐备（软/硬/生长），生长模式的种子推进链可查 |
| `fresnel_edge` | 3D（2D 用到边距离伪菲涅尔） | `power`(0.5~8)、`edgeColorSlot(hot/primary/cool)`、`rimOnly(bool)` | 材+0（法线运算） | M-8：3D 路径用真法线·视线；2D 路径用 SDF 边距离（两路都存在） |
| `vertex_displace` | 3D 为主（2D quad 顶点亦可） | `mode(tongue/bulge/sway/rise/tension/gerstner)`、`amplitude`、`frequency`、`axisMask` | 仅 PH 默认开（原型目录 §2.4"3+顶点位移"）；其余档 amplitude 截 0 | M-9：六模式枚举齐备；amplitude=0 时输出恒等（降级安全） |
| `smoothmin_union` | both | `blobCount`(≤8)、`smoothK`、`highlightWidth`(边高光线) | 材1~2 | M-6；M-10：平滑并集 k 参数存在 |
| `multiply_dark_core` | both | `darkness`(乘算强度)、`edgeGlowColor`、`edgeGlowWidth` | 材1 + 1 透明层（乘算与加色双通道） | M-11：乘算通道与加色边通道并存（暗元素反向发光结构） |
| `glow_radial_layered` | both | **§3bis 全参数面**：`glowSize / glowFalloff / glowSoftness / glowIntensity / glowColorInner / glowColorOuter / glowPulseMode / glowPulseRate / glowPulseDepth / glowShape(circle/ellipse/stretched) / glowLayers(1~3) / glowLayerRatio / glowSoftDepthFade / glowFlickerCoupling` | ML 1 层 → MM/MH 2 层 → PL+ 按 `glowLayers`（≤3）；每层 1 透明叠加计入 overdraw 预算 | M-12：全部 §3bis 参数为材质属性（逐名断言）；M-13：耦合补偿链存在（glowSize↑ 时有效强度不衰减，shader 内 `glowIntensity * pow(glowSize, k)` 补偿项可查）；M-14：`stretched` 形状的运动轴拉伸端口存在；六档最低档 `glowLayers` 截 1 |
| `refraction_layer` | both（3D 效果完整，2D 退化为 UV 微扰高光） | `strength`(0~1)、`normalNoiseScale`、`tintSlot` | 材2 + （真折射路径）1 次 Scene Color 采样 | M-15：双路径并存——URP Opaque Texture 可用时走 Scene Color 真折射，不可用时自动退回法线扰动，两路无错误节点（ADR-010 §4bis-1）；运行时探测组件谓词见 §7 |

**子图资产表**（材质变体的内部构件，T2A_REPORT §5.1-1 点名清单全覆盖）：

| 子图 | 服务的变体 |
|---|---|
| `SG_PolarUV`（极坐标 UV） | sdf_shape(ring/fan) / sdf_rune_ring / sdf_sacred_pattern / 光晕形状 |
| `SG_FBM2`（双层 FBM，速比+各向异性拉伸端口） | noise_fbm_2layer / refraction_layer / threshold_dissolve 宿主 |
| `SG_VoronoiCrystal` | noise_voronoi_crystal |
| `SG_Jagged1D` | noise_jagged_1d |
| `SG_SDFShapes`（圆/环/星/多边形/扇/圆角矩形/叶形/十字） | sdf_shape / sdf_sacred_pattern |
| `SG_SDFCrackBranch` | sdf_crack_branch |
| `SG_ThresholdTri`（soft/hard/grow 三型阈值） | threshold_dissolve 与全部需要阈值消散的变体 |
| `SG_FresnelEdge`（3D 法线路 + 2D SDF 边距离路） | fresnel_edge |
| `SG_SmoothMin` | smoothmin_union |
| `SG_GlowFalloff`（径向衰减 + 噪声打散 + 各向异性 + 层比 + 耦合补偿） | glow_radial_layered |
| `SG_SceneColorSafe`（Opaque Texture 探测双路） | refraction_layer |
| `SG_StyleStage`（插槽约定）+ `SG_Style_Cartoon` / `SG_Style_Pixel` / `SG_Style_None` | 全部材质变体末端 |

## 4. GPU 粒子族（VFX Graph）——10 场模板

### 4.1 路线裁定（登记）

`.vfx` 资产是 Unity 私有可视图序列化，手写 YAML 极脆（节点 GUID/槽位索引跨版本漂移），且本工程 `manifest.json` **未引入 `com.unity.visualeffectgraph`**（编辑器内置包可用但工程未启用）。T2b 裁定走**「可编程模板描述 + 编译器生成」**路线：

- 每个场模板 = 一份 C# 模板描述（`VfxFieldTemplate`，ScriptableObject：场类型、参数面、输出形、碰撞、事件分裂、像素吸附块开关）。
- 编译器按描述生成**运行档位允许的实现**：MH+ 档在工程启用 VFX Graph 后由 Editor 脚本化构建 `.vfx`（`T3 落地`）；当前 T2b 交付模板描述资产 + 参数面 + 门禁 + **CPU 粒子降级实现**（§5 与 GPU 场一一对应，保证任何档位可编译出运动语义一致的层）。
- 该裁定不缩小覆盖面声明：GPU 族的"实现规格 + 骨架资产（模板描述）+ 变体清单 + 门禁谓词"四件齐备；`.vfx` 图的脚本化构建属 T3 逐族落地范围（ADR-010 §11 卡序）。

### 4.2 变体表

输出形（全模板共有参数）：`renderShape(stretchedBillboard/quad/mesh)`；碰撞：`collision(none/plane/sdf)`；事件分裂：`splitEvent(none/onDeath/onCollide, splitCount)`；像素风：`pixelSnap(bool, pixelWorldSize)`（Output 前位置吸附栅格，风格约束注入）。

预算对齐原型目录 §2.4：GPU 粒子 ML/MM 禁用（自动降级 CPU 谱）→ MH ≤2k → PL ≤5k → PM ≤20k → PH ≤100k。

| 变体 id | 2D/3D | 场参数面 | 门禁谓词 |
|---|---|---|---|
| `field_buoyancy_turbulence` | both（2D 压平 XY） | `buoyancy`、`curlFreq`、`curlStrength`、`lifetime(range)`、`sizeCurve(shrink/grow)` | F-1：浮力项 >0 且方向 +Y；F-2：湍流场节点存在 |
| `field_gravity_settle` | both | `gravity`、`bounce`(0~1)、`settleTime`(停留)、`friction` | F-3：重力 >0；F-4：碰撞后停留链存在 |
| `field_instant_rephase` | both | `rephaseRate`(Hz)、`jumpRadius`、`lifetime`(极短 0.05~0.3) | F-5：位置更新走阶跃（无插值惯性链） |
| `field_gravity_drag_split` | both | `gravity`、`drag`、`splitCount`(2~6)、`splitSizeRatio` | F-3；F-6：分裂事件链存在且 splitCount 受档位截断 |
| `field_vortex` | both（2D 切向场投影） | `tangentSpeed`、`radialPull`、`axialLift`、`coneAngle` | F-7：切向+向心+轴向三分量齐备 |
| `field_orbital_hover` | both | `orbitRadius`、`orbitSpeed`、`hoverAmplitude`、`phaseJumpPeriod` | F-8：轨道场存在；重力恒 0 |
| `field_step_grid` | both | `stepInterval`、`gridSize`、`flashOnArrive(bool)` | F-9：位移按 `floor(t/interval)` 阶跃 |
| `field_attract_target` | both | `attractStrength`、`arriveRadius`、`spiralBias` | F-10：吸引目标位置属性存在且可运行时绑定 |
| `field_drift_curl` | both | `driftDir(vec3)`、`curlFreq`、`curlStrength`(弱) | F-2 |
| `field_fall_wind` | both | `fallSpeed`、`windDir(vec2)`、`gustAmplitude`、`groundFade` | F-3；F-11：接地淡出链存在 |

## 5. CPU 粒子族（ParticleSystem）——11 模块谱

每变体 = 一份模块谱（哪些 ParticleSystem 模块开、参数怎么算），由 `CpuParticleRecipeApplier` 应用到 ParticleSystem 组件。与 GPU 场一一对应（`ps_` 前缀），保证降级语义一致；另有 `ps_burst_streaks`（瞬发短条爆发，节点闪光/溅射通用）。

预算：ML ≤60 / MM ≤150 / MH ≤300 / PL ≤400 / PM ≤600 / PH ≤1000（峰值粒子，全预制体累计）。

| 变体 id | 模块谱要点 | 门禁谓词 |
|---|---|---|
| `ps_buoyancy_turbulence` | VelocityOverLifetime(+Y 递增) + Noise(中频中强) + ColorOverLifetime + SizeOverLifetime | P-1：Noise 或 VelocityOL 至少一个启用（禁静止喷点）；P-2：ColorOverLifetime 启用且 alpha 有衰减 |
| `ps_gravity_settle` | gravityModifier>0 + Collision(world, bounce 低, lifetimeLoss) + RotationOverLifetime | P-3：gravityModifier>0；P-2 |
| `ps_instant_rephase` | Emission Bursts 多脉冲 + 极短寿命 + 高速随机方向 + Stretched Billboard | P-4：burst 数 ≥2；寿命上限 ≤0.3 s |
| `ps_gravity_drag_split` | gravity + LimitVelocityOverLifetime(drag) + SubEmitters(Collision/Death) | P-3；P-5：SubEmitter 链存在 |
| `ps_vortex` | VelocityOverLifetime Orbital(Y) + radial 负 + Noise | P-6：orbital 分量非零 |
| `ps_orbital_hover` | Orbital + gravity 0 + 长寿命 + 尺寸恒定 | P-6；gravityModifier==0 |
| `ps_step_grid` | 材质顶点吸附（渲染侧）+ Color Fixed 渐变 + Rotation 0/90° | P-7：颜色渐变 Fixed 模式 |
| `ps_attract_target` | VelocityOL 曲线向目标 + 寿命末端加速 | P-8：速度曲线非常量 |
| `ps_drift_curl` | 弱 VelocityOL + Noise 弱 + 长寿命大粒子低 alpha | P-1 |
| `ps_fall_wind` | VelocityOL(-Y 恒定 + 侧风) + Noise 阵风 + 碰撞消亡 | P-3 或 -Y 速度非零 |
| `ps_burst_streaks` | Bursts 单脉冲 + Stretched Billboard + 短寿命 + 高初速 | P-4（burst ≥1）；P-2 |

## 6. 网格几何族——19 生成器

统一接口 `IProceduralMeshGenerator`：`Generate(MeshGenParams p) → Mesh`，`p` 含确定性 `seed`、`dimension(2d/3d)`、变体参数块；实现必须满足 G-3 确定性与下表谓词。顶点预算对齐 §2.4：ML ≤256 / MM ≤512 / MH·PL ≤2k / PM ≤8k / PH ≤32k（每层）；生成器接受 `vertexBudget` 输入并自动降细分。

| 变体 id | 2D/3D | 参数面 | 门禁谓词 |
|---|---|---|---|
| `crystal_cluster` | both（2D 退化为平面棱柱轮廓组） | `spikeCount`、`lengthRange`、`radiusRange`、`hexSides`、`spreadAngle` | X-1：顶点数>0 且 ≤预算；X-2：包围盒有限且尖端在 `lengthRange` 内；G-3 |
| `rock_chunk` | both | `noiseAmplitude`、`noiseFreq`、`hardEdgeAngle` | X-1；X-3：低频噪声位移后无 NaN 顶点 |
| `jagged_polyline` | both | `segments`、`jitter`、`forkDepth`(0~3 递归分叉)、`forkAngle`、`bandWidth`、`start/end(vec3)` | X-1；X-4：分叉数 = 按 forkDepth 的期望树结构（递归可查）；X-5：端点精确等于 start/end |
| `spiral_ribbon` | both | `turns`、`radius`、`pitch`、`width`、`taper` | X-1；X-2 |
| `tendril_blob` | 3D 为主（2D 轮廓版） | `tendrilCount`、`tendrilLength`、`blobRadius`、`waviness` | X-1；X-2 |
| `wire_polyhedron` | both | `polyType(octa/icosa/cube)`、`wireRadius`、`subdiv` | X-1；X-6：棱线数与所选多面体拓扑一致 |
| `tech_panel` | both | `cornerRadius`、`bevel`、`size(vec2)`、`inset` | X-1 |
| `vine_spline` | both | `controlPoints[]`、`tubeRadius`、`leafCount`、`leafSize` | X-1；X-5（样条过端点） |
| `branch_tree` | both | `depth`(1~4)、`branchAngle`、`lengthDecay`、`radiusDecay` | X-1；X-4 |
| `sweep_band` | both | `arcAngle`、`radius`、`width`、`arcSegments`（近战扫掠面） | X-1；X-2 |
| `catenary_band` | both | `sag`、`start/end`、`width`、`segments` | X-1；X-5；X-7：中点下垂 = sag（悬链近似可查） |
| `parabola_tube` | both | `start/end`、`apexHeight`、`tubeRadius`、`flowTaper` | X-1；X-5 |
| `splash_crown` | 3D 为主 | `crownPoints`、`crownHeight`、`baseRadius`、`irregularity` | X-1；X-2 |
| `shell_polyhedron` | both（2D 圆环壳） | `shellShape(sphere/dome/capsule)`、`facetCount`、`radius` | X-1；X-8：壳封闭或半封闭（边界边数可查） |
| `ring_torus_segments` | both（2D 极坐标环退化为平面环带） | `majorRadius`、`minorRadius`、`segments`、`gapRatio`（薄环：REFERENCE_ANALYSIS C 组） | X-1；X-9：minor/major 比 ≤0.25（"薄"可查，默认值断言） |
| `cylinder_beam` | both（2D 双 quad） | `length`、`radius`、`capSoftness`、`radialSegments` | X-1；X-5（两端受控） |
| `voronoi_prefracture` | **both**（3D 凸块 / 2D 多边形切片） | `fragmentCount`、`sourceBounds`、`thickness(2D)`、`coreBias` | X-1；X-10：碎块数 = fragmentCount 且各块体积>0；块数受档位 `块:n` 截断 |
| `subdivided_plane` | both | `size(vec2)`、`subdivX/Y` | X-1；X-11：细分数受顶点预算截断 |
| `radial_spike_array` | both | `spikeCount`、`lengthRange`、`widthRange`、`angleJitter`、`innerRadius`、`planarity(3D 球面/平面分布)`（放射光针阵列，REFERENCE_ANALYSIS B 组，ADR-010 §4bis-8） | X-1；X-12：针数 = spikeCount、长度界内、随机角受 seed 确定 |

## 7. 局部光族——1 节拍器变体

| 变体 id | 2D/3D | 参数面 | 门禁谓词 |
|---|---|---|---|
| `light_beat` | 3D→URP `Light`(Point/Spot)、2D→`Light2D`(Point)、`光:烘` 档→写材质发光倍率 | `color`、`intensity`、`range`、`flickerMode(steady/breathe/flicker/pulse/strobe)`、`flickerRate`、`flickerDepth`、`decayShape(exp/linear/step/smooth/spike)`、`castShadows`；风格量化：`intensitySteps`、`flickerQuantize`、`frameRate` | L-1：非烘档必须存在 Light/Light2D 组件且节拍器组件在同节点；L-2：`flickerMode≠steady` 时强度曲线非常量（节拍器序列化字段可查）；L-3：烘档无 Light 组件但材质发光绑定存在；L-4：光晕层 `glowFlickerCoupling=true` 时其 pulse 相位源指向本组件（同源同步，序列化引用可查） |

档位截断（§2.4）：ML 烘 → MM/MH/PL 1~2 盏无阴影 → PM 3 盏 ≤1 阴影 → PH 4 盏 ≤2 阴影。`castShadows` 与档位取小。

## 8. 成本模型六档汇总（编译器 E300 预算校验的数据源）

预算基线 = 原型目录 §2.4 表（GPU 粒子 / CPU 粒子峰值 / 材质噪声层 / 局部光数 / 破碎块 / Cloth / 透明叠加 / 网格顶点八项）。本清单各变体的成本列给出该变体在每项上的记账方式：

| 预算项 | 记账规则 |
|---|---|
| GPU 粒子 | `field_*` 变体的 `capacity` 参数直接记账；ML/MM 强制 0（族替换为 `ps_*`） |
| CPU 粒子峰值 | `ps_*` 变体按 `maxParticles`（含 burst 峰值）累计 |
| 材质噪声层 | 材质变体成本列的 `材n`；`glow_radial_layered` 每层记 1 透明叠加 + 0 噪声层（衰减是解析式） |
| 局部光数 | `light_beat` 实例数；烘档记 0 |
| 破碎块 | `voronoi_prefracture.fragmentCount` |
| Cloth | 布料变体档位（v1 由 `subdivided_plane` + 顶点位移承担，Cloth 组件 T3 引入） |
| 透明叠加层 | 每个透明材质层 +1；`glowLayers` 逐层 +1；`multiply_dark_core` +2（乘算+加色） |
| 网格顶点 | 生成器输出 `mesh.vertexCount`，受 `vertexBudget` 截断 |

## 9. 审计清单

- [ ] 5 族变体表齐备，id 与 `recipe-v2.schema.json` 闭集枚举严格一致。
- [ ] T2A_REPORT §5.1 点名的子图/场模板/生成器全部有归属（子图资产表 / §4 / §6）；`radial_spike_array` 与光晕层已入表。
- [ ] 每变体四列齐备（2D/3D、参数面、成本、谓词）；谓词全部构造性可查。
- [ ] 正文无具体特效名；无序列帧语义（G-1 为门禁第一条）。
