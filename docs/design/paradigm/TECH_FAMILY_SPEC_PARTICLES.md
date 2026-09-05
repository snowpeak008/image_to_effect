# 技术族实现规格：GPU 粒子族（VFX Graph）+ CPU 粒子族（ParticleSystem）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）、`docs/design/references/REFERENCE_ANALYSIS.md`（质感标尺）
配套文档：`PROTOTYPE_CATALOG_v1.md`、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`TECH_FAMILY_SPEC_MATERIAL.md`（粒子材质由其子图组成）、`TECH_FAMILY_SPEC_MESH_LIGHT.md`、`STYLE_IMPL_CARTOON_PIXEL.md`、`COMPILER_BOUNDARY_V2.md`

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **技术族轴** | 5 族中的第 2、3 族（GPU 粒子 `gpu_particles` / CPU 粒子 `cpu_particles`）**全部实现规格**：14 个 VFX Graph 模板、14 个 CPU 模块组合（逐模板一一对照的降级映射）、像素风栅格吸附的实现位置、2D 排序限制与应对、UGUI Overlay 伪粒子规格、构造性谓词 |
| **原型轴** | 不新增、不修改原型；本两族服务 21 个层角色中的 **9 个**（`emission`（含全部后缀）/ `body` / `trail` / `debris` / `orbit` / `shock`（气浪类）/ `veil`（飘动颗粒类）/ `core`（稠密粒子构成的核）/ `ground`（贴地颗粒））；其中 `emission.*` 是本两族的绝对主场（层角色词表里出现频次最高的角色，58 原型中共 123 处） |
| **元素轴** | 13 元素 + `none` 的 5 个物理子画像在两族的预设倾向全部有模板归属（§7 元素→模板速查表） |
| **风格轴** | 首批 2 的粒子约束（`STYLE_CATALOG_v1.md` §2.3 / §3.3）落到具体实现位置：卡通 = 形状替换 + 尺寸分档 + Fixed 色阶；像素 = Output 前位置量化 + 方块形 + 90° 旋转量化（§6） |
| **维度轴** | 每模板声明支持维度；2D 的排序限制与"何时必须用 CPU 粒子"给出判定规则（§5） |
| **档位轴** | 六档全部有降级机制：GPU 模板在 ML/MM 一律不可用（`PROTOTYPE_CATALOG_v1.md` §2.4：GPU 粒子在 ML/MM 为"禁用"），逐模板给出 CPU 替代（§4）；CPU 模板按峰值上限截断（§4.4） |

**纪律**：正文只用层角色 / 技术族 / 参数表述，无具体特效名；无序列帧 / flipbook / sprite sheet 任何形式——粒子的**形状一律由材质族的 SDF 子图程序化产生**（`TECH_FAMILY_SPEC_MATERIAL.md` §3.2），不使用 Texture Sheet Animation 模块的动画功能。

---

## 1. 两族的分工与选择规则

### 1.1 为什么是两族而不是一族

ADR-010 §4 把 GPU 粒子与 CPU 粒子列为两个独立技术族，理由在实现层非常具体：

| | GPU 粒子（VFX Graph） | CPU 粒子（ParticleSystem） |
|---|---|---|
| 密度上限 | 10⁴~10⁵ | 10²~10³ |
| 力场表达 | 图内任意计算（curl / 向量场 / SDF 碰撞 / 吸引排斥） | 固定模块组合（Velocity / Force / Noise / Limit） |
| 2D 排序 | **每个 VisualEffect 是一个整体**，无法与 SpriteRenderer 逐层交错（§5.1） | `ParticleSystemRenderer` 有 `sortingLayerID` / `sortingOrder`，可精确插入 2D 层序 |
| 事件与子发射 | GPU Event / Spawn Event，回读困难 | Sub Emitter + `OnParticleCollision` 回调可回读 |
| 移动端 | Compute Shader 依赖，低端设备不可靠 | 全平台可用 |
| 确定性 | 依赖 GPU 执行顺序，逐帧完全一致性不保证 | `useAutoRandomSeed=false` 时可复现 |

因此两族不是"同一能力的高低配"，而是**不同的能力集**，只在"高密度轻量颗粒"这一交集上互为降级目标。

### 1.2 编译期选族规则（fail-closed）

```
若 layer.technique.family == gpu_particles:
    若 tier ∈ {ML, MM}                       → 按 §4 映射表替换为 cpu_particles + 对应变体（登记降级）
    否则若 dimension == 2d 且 该层需要与其他层交错排序（§5.2 判定）
                                              → 替换为 cpu_particles（登记降级）
    否则若 该层挂在 Overlay Canvas 下（F 类）  → 替换为 material 伪粒子（§8，登记降级）
    否则                                      → 保持 GPU
若 layer.technique.family == cpu_particles:
    若 tier ∈ {PM, PH} 且 recipe 给了 tierOverrides 升级 → 按 override 升为 GPU
    否则                                                  → 保持 CPU
```

三条降级路径**都不报错**（与折射可选层同一纪律），全部在编译报告登记；但降级后的产物必须仍满足该层的构造性谓词（§9），否则 FAIL。

---

## 2. GPU 粒子族：VFX Graph 模板库

### 2.1 模板的结构契约

每个 VFX Graph 模板（`.vfx` 资产）是**一条完整的 System**，结构固定为四块：

```
Spawn      → 由控制器通过 SendEvent 驱动（不 PlayOnAwake；见 §2.4）
Initialize → 位置/方向/速度/尺寸/寿命/颜色的初值（全部由 Exposed Property 参数化）
Update     → 主力场（模板的身份所在）+ 碰撞 + 事件触发
Output     → 输出形（拉伸 billboard / quad / 网格粒子）+ 材质（Shader Graph，来自材质族）
```

**模板的身份 = Update 块里的主力场**。Initialize / Output 的差异由参数与 Output 形状枚举承担，不构成新模板。这与材质族"主图闭集 + 子图组合"是同一思路，目的同样是让资产白名单是小闭集（`COMPILER_BOUNDARY_V2.md` §3）。

### 2.2 全模板统一暴露属性（Exposed Properties）

| 属性 | 类型 | 来源 | 语义 |
|---|---|---|---|
| `SpawnRate` | float | recipe / 元素 | 持续发射率（个/秒） |
| `BurstCount` | int | recipe / 元素 | 单次爆发数 |
| `Capacity` | uint | 档位预算 | 容量上限（编译期烘死，**不是运行时参数**） |
| `Lifetime` | Vector2 | 元素 | 寿命范围（min, max） |
| `StartSize` | Vector2 | 元素 | 初始尺寸范围 |
| `SizeOverLife` | AnimationCurve | 元素 | 尺寸曲线 |
| `PaletteA…E` | Vector4（HDR 线性） | 元素色板 | 与材质族同源（`TECH_FAMILY_SPEC_MATERIAL.md` §2.4） |
| `ColorOverLife` | Gradient | 元素 | 颜色梯度；卡通 / 像素风改为 Fixed 模式（§6.2） |
| `Intensity` | float | 标准参数 | 亮度倍率 |
| `Scale` | float | 标准参数 | 空间尺度倍率（作用于发射体尺寸与速度） |
| `Speed` | float | 标准参数 | 时间倍率（作用于 `VisualEffect.playRate`） |
| `Seed` | uint | 标准参数 | 固定随机种子（**必须**设 `VisualEffect.startSeed` 并关闭 `resetSeedOnPlay`，保证截帧确定性） |
| `EmitShape` | 见各模板 | 元素/原型 | 发射体形状参数 |
| `FieldStrength` | float | 元素 | 主力场强度（模板专有语义） |
| `PixelSnapSize` | float | 风格 | 0 = 不吸附；> 0 时 Output 前量化（§6.1） |
| `BeatValue` | float | 局部光节拍器 | 与光同呼吸的耦合输入（用于亮度/发射率脉冲） |

### 2.3 输出形（Output 形状）闭集

| 形 | VFX Graph Output | 用途 | 备注 |
|---|---|---|---|
| `stretchedBillboard` | Output Particle Quad + Orient: Along Velocity + Size Y 按速度缩放 | 火星 / 液滴 / 电弧 / 丝 | 长宽比上限由风格约束（卡通 3:1） |
| `quad` | Output Particle Quad + Orient: Face Camera | 团 / 雾 / 泡 / 叶 | 形状来自材质 SDF |
| `meshParticle` | Output Particle Mesh | 碎晶 / 碎石 / 几何符号 / 方块 | 网格来自网格族生成器（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4），**不是导入模型** |
| `strip` | Output Particle Strip（Quad Strip） | 带状尾流 / 丝带 | 需 `Initialize Particle Strip` |

四种输出形之外**不允许**其他 Output 类型（尤其禁止任何依赖 Flipbook 的 Output 配置，谓词 GP-6）。

### 2.4 播放控制契约

- 模板**不得**勾选 `Play On Awake`（否则预制体一实例化就开始播，破坏控制器对阶段的所有权）。
- 模板必须暴露名为 `OnPlay` / `OnStop` 的 Event，控制器通过 `VisualEffect.SendEvent()` 驱动。
- 池化复位（`COMPILER_BOUNDARY_V2.md` §5.5）调用 `VisualEffect.Reinit()`，模板不得依赖任何跨复位的持久状态。

### 2.5 粒子材质与辉光

粒子的渲染材质由材质族的子图组成（`mat_shape_sdf` / `mat_leaf_petal` / `mat_volume_fbm` 等）。**粒子层不生成 `Glow_k` 子节点**（`TECH_FAMILY_SPEC_MATERIAL.md` §5.11）：光晕由粒子材质自身的软边 + 加法混合承担。理由：每颗粒子挂 4 层辉光在成本模型上不可接受，且粒子的"离散高亮点极少但极亮"（REFERENCE_ANALYSIS §2-5）本就是靠**数量克制 + 亮度顶格**达成，不靠光晕层数。

---

## 3. VFX Graph 模板清单（14 个）

以 `T2A_REPORT.md` §5.1-2 的力场归类为起点，合并同构力场、补齐层角色缺口后为 14 个。

| # | 模板 id | 主力场（Update 块的身份） | 输出形 | 碰撞 | 事件 | 参数面（除 §2.2 统一属性外） | 维度 | 成本档位 | 服务层角色 / 元素 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `vfx_buoyancy_turbulence` | 向上浮力（`+worldUp * buoyancy`）+ Curl 湍流（频率/强度可调）+ 线性阻力 | `stretchedBillboard` / `quad` | 可选平面（触地熄灭） | 寿命末尾按概率分裂为 N 个更小者（GPU Event） | `buoyancy, turbFreq, turbStrength, drag, splitProbability, splitCount, splitScale` | 2d,3d | MH+ | `emission.sparks / body / trail / core`；`fire`（主）、`light`、`nature`（孢子） |
| 2 | `vfx_gravity_settle` | 重力 + 阻力 + 地面停留（触面后速度归零并进入淡出计时） | `meshParticle` / `stretchedBillboard` | 平面 / SDF（可选反弹与摩擦） | 触面事件（触发 decal 层的生成信号） | `gravity, drag, bounciness, friction, settleFade, restRotationLock` | 2d,3d | MH+ | `emission.chips / debris / trail`；`ice`、`earth`、`blood`、`none.rubble` |
| 3 | `vfx_instant_rephase` | **无连续运动**：每 `rephaseRate` 次/秒把全部存活粒子瞬移到新的随机位置（沿声明的形状/线段重采样），位置不插值 | `stretchedBillboard`（短） | 无 | 生成时分裂为 2~3 子体沿随机方向 | `rephaseRate, jumpRadius, alongPath(bool), pathSegments, branchCount` | 2d,3d | MH+ | `emission.* / trail / edge`；`lightning`（主）、`tech`（故障态） |
| 4 | `vfx_gravity_drag_split` | 重力 + 强阻力（粘滞）+ 碰撞分裂 | `stretchedBillboard`（沿速度拉伸比随速度） | 平面 / SDF（触面摊开并消） | 碰撞时分裂为 3~6 更小者 + 触面 decal 信号 | `gravity, viscousDrag, stretchRatio, splitOnCollision, splitCount, splitSpeedFactor` | 2d,3d | MH+ | `emission.drops / trail`；`water`、`poison`、`blood`、`none.liquid` |
| 5 | `vfx_vortex` | 涡旋场：切向速度 + 向心分量 + 轴向上升，叠强湍流 | `stretchedBillboard`（长） / `quad` | 无 | 无 | `axis, tangential, centripetal, axialRise, radiusRange, turbStrength` | 2d,3d | MH+ | `emission.* / body / orbit / veil`；`wind`（主）、`shadow`（向心版，参数取负） |
| 6 | `vfx_orbital_hover` | 轨道场：绕轴角速度 + 半径弹簧（回到目标半径）+ 零重力悬浮 + 周期相位脉冲 | `meshParticle` / `quad` | 无 | 相位脉冲时全体亮度峭点（属性脉冲，不是新粒子） | `orbitAxis, angularSpeed, targetRadius, radiusSpring, hoverAmplitude, phasePeriod, phaseGain` | 2d,3d | MH+ | `orbit / emission.* / body`；`arcane`（主）、`light`、`tech` |
| 7 | `vfx_step_grid` | 阶跃位移：每 `stepRate` 次/秒沿速度方向跳一格 `cellSize`，格间不插值；可选沿曼哈顿路径 | `quad`（方） / `meshParticle` | 无 | 到达格点时短闪（属性脉冲） | `cellSize, stepRate, manhattan(bool), pathAxisOrder, flashGain` | 2d,3d | MH+ | `emission.* / trail / body`；`tech`（主）、像素风的通用替代 |
| 8 | `vfx_attract_target` | 向目标点吸引（可配加速度或恒速）+ 到达半径内消失 + 螺旋偏置 | `stretchedBillboard` / `quad` | 无 | 到达目标时发出汇聚事件（供 `flash` 层接） | `targetPosition, attractAccel, maxSpeed, arriveRadius, spiralBias, spawnShellRadius` | 2d,3d | MH+ | `emission.* / orbit / body`；蓄力 / 拾取 / 治疗类语义；全元素通用 |
| 9 | `vfx_drift_curl` | 极弱重力 + Curl 场主导 + 长寿命 + 大范围包围盒 | `quad`（大而淡） | 无 | 无 | `curlFreq, curlStrength, gravityBias, boundsSize, recycleMode` | 2d,3d | MH+ | `body / veil / emission.ambient`；`none.dust`、`wind`、`fog` 类语义 |
| 10 | `vfx_fall_wind` | 恒定下落速度 + 水平风（可随高度变化）+ 逐粒子摆动相位 | `stretchedBillboard`（雨） / `quad`（雪/叶） | 可选平面（触地生成小溅） | 触地事件 | `fallSpeed, windVector, windGradient, swayAmplitude, swayFreq, spawnVolume` | 2d,3d | MH+ | `emission.precip / body`；天气语义；`water`、`ice`、`nature` |
| 11 | `vfx_burst_radial` | 单次径向爆发：初速沿球面/圆周方向 + 强阻力衰减，无持续力场 | `stretchedBillboard` / `meshParticle` | 可选平面 | 无 | `burstSpeed, speedSpread, coneAngle, drag, radialBias` | 2d,3d | MH+ | `emission.debris / emission.sparks / shock`；全元素的爆发瞬间 |
| 12 | `vfx_strip_trail` | 沿宿主运动路径生成粒子条带（Particle Strip），宽度沿寿命衰减 | `strip` | 无 | 无 | `stripLength, widthCurve, sampleRate, uvMode, jitter` | 2d,3d | PL+ | `trail / link / beam_column`；全元素 |
| 13 | `vfx_surface_scatter` | 在宿主网格表面/SDF 表面按面积均匀散布，沿法线微飘，跟随宿主变换 | `quad` / `meshParticle` | 无 | 无 | `sourceMode(mesh/sdf), density, normalOffset, driftSpeed, followMode` | 3d（2D 退化为轮廓散布） | PL+ | `body / emission.surface / edge`；侵蚀 / 附着 / 覆盖语义 |
| 14 | `vfx_mesh_shatter` | 从宿主网格三角面生成网格粒子并按爆炸/重力驱动（**轻量破碎**，与真刚体预破碎互补） | `meshParticle` | 平面（可选） | 无 | `explodeSpeed, spin, gravity, fadeMode, sourceTriangleStride` | 3d | PM+ | `debris / emission.chips`；`mesh_shell.dissolve` 的粒子化路线 |

**模板命名规则**：`vfx_<主力场语义>`，`^[a-z][a-z0-9_]*$`。**禁止**在模板名中出现元素名或原型名。

---

## 4. CPU 粒子族：逐模板降级映射

### 4.1 CPU 族的产物形状

```
Layers/<layerId>
├─ ParticleSystem（主）
│  ├─ Main：startLifetime / startSpeed / startSize / startColor / gravityModifier /
│  │        maxParticles = Capacity / simulationSpace / useAutoRandomSeed=false / randomSeed=Seed
│  ├─ 按模板启用的模块（见 §4.3）
│  └─ ParticleSystemRenderer：renderMode / material / sortingLayerID / sortingOrder / alignment
└─ SubEmitters（可选，0~2 个子 ParticleSystem）
```

### 4.2 逐模板对照表（GPU 模板 → ParticleSystem 模块组合）

| GPU 模板 | CPU 变体 id | 启用模块与关键设置 | 能力损失（诚实记录） |
|---|---|---|---|
| 1 `vfx_buoyancy_turbulence` | `cpu_buoyancy_turbulence` | `VelocityOverLifetime`（Y 递增曲线 = 浮力）；`Noise`（frequency 中、strength 中、quality Medium、`damping` 开）；`LimitVelocityOverLifetime`（dampen = drag）；`ColorOverLifetime`；`SizeOverLifetime`；`Collision`（World，bounce 0.2，lifetimeLoss 0.5）；`SubEmitters`（Death，概率由 `EmissionModule` 的 burst 概率近似） | Curl 场退化为 Perlin `Noise` 模块（无散度不保证，翻腾感略弱）；分裂概率无法逐粒子随机，用 Death SubEmitter 的固定比例近似 |
| 2 `vfx_gravity_settle` | `cpu_gravity_settle` | `Main.gravityModifier`；`LimitVelocityOverLifetime`（drag）；`Collision`（World，bounce/friction，`lifetimeLoss=0`，`enableDynamicColliders` 关）；`RotationOverLifetime`；`ColorOverLifetime`（末段硬切）；`Renderer.renderMode = Mesh` | 停留后的"绝对静止"需 `Collision.dampen=1`，仍可能有微滑动；SDF 碰撞不可用（只有平面/世界碰撞） |
| 3 `vfx_instant_rephase` | `cpu_instant_rephase` | 不做位移，改为**高频短寿命重生**：`Emission.bursts`（多次，间隔 = `1/rephaseRate`）；`Main.startLifetime = 1/rephaseRate`；`Shape`（Edge/Mesh，沿路径）；`SubEmitters`（Birth → 子弧）；`Renderer.renderMode = Stretched Billboard` | "同一批粒子瞬移"变成"整批重生"，粒子 id 不连续（视觉上等价，因为本就无插值）；容量利用率低 |
| 4 `vfx_gravity_drag_split` | `cpu_gravity_drag_split` | `Main.gravityModifier`；`LimitVelocityOverLifetime`（强 dampen）；`Renderer.renderMode = Stretched Billboard`（`velocityScale` = stretchRatio）；`Collision`（World，`lifetimeLoss=1`）；`SubEmitters`（Collision → 小滴 + decal 信号） | 分裂数固定（无逐粒子随机范围） |
| 5 `vfx_vortex` | `cpu_vortex` | `VelocityOverLifetime`（`orbitalVelocity` 绕轴 + `radial` 负 + linear Y）；`Noise`（strong）；`RotationOverLifetime`；`Trails`（可选） | 轨道中心固定为系统原点（GPU 版可任意轴任意中心）；半径分布控制弱 |
| 6 `vfx_orbital_hover` | `cpu_orbital_hover` | `VelocityOverLifetime.orbitalVelocity` + `orbitalOffset` + `radial=0`；`Main.gravityModifier=0`；`ColorOverLifetime`（多峰渐变近似相位脉冲） | 半径弹簧不可用（轨道半径固定）；相位脉冲用颜色曲线近似，不是真的全体同步峭点 |
| 7 `vfx_step_grid` | `cpu_step_grid` | **材质承担阶跃**：`VelocityOverLifetime` 恒速 + 顶点着色器内 `floor(pos/cellSize)*cellSize`（`mat_shape_sdf` 的 `PixelSnapSize` 通路，§6.1）；`RotationOverLifetime` 量化到 90° | 到达格点的闪光事件不可用（无回调），改由材质内按量化后位置变化的时刻做亮度脉冲近似 |
| 8 `vfx_attract_target` | `cpu_attract_target` | `VelocityOverLifetime`（不足以吸引）→ 用 `ExternalForces` + 场景内 `ParticleSystemForceField`（**不可用：力场是场景对象，违反自包含**）→ 实际方案：`Main.simulationSpace = Local` + `VelocityOverLifetime.radial` 取负（向系统原点收缩）+ 把系统原点绑定到目标 | 目标必须是系统原点（把 `targetPosition` 变成层节点的位置，由控制器每帧写 `transform.position`）；螺旋偏置用 `orbitalVelocity` 叠加 |
| 9 `vfx_drift_curl` | `cpu_drift_curl` | `Noise`（低频、低强度、`quality Low`）；`Main.gravityModifier` 微小；`Main.simulationSpace = World`；大 `Shape.Box` | 无 |
| 10 `vfx_fall_wind` | `cpu_fall_wind` | `Main.startSpeed` + `gravityModifier`；`VelocityOverLifetime`（水平常量 = 风）；`Noise`（低频，作摆动）；`Shape.Box`（顶部体积）；`Collision`（World）+ `SubEmitters`（Collision → 小溅） | 风随高度变化不可用（`windGradient` 丢失，用固定风近似） |
| 11 `vfx_burst_radial` | `cpu_burst_radial` | `Emission.bursts`（1 次）；`Shape`（Sphere/Circle/Cone，`randomDirectionAmount`）；`LimitVelocityOverLifetime`（dampen）；`SizeOverLifetime`；`ColorOverLifetime` | 无（本模板 CPU 与 GPU 表达能力几乎等价，只差密度） |
| 12 `vfx_strip_trail` | `cpu_strip_trail` | **不用 ParticleSystem**：降级为网格族的 `TrailRenderer`（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.20）。若必须留在粒子族，用 `Trails` 模块（`Ribbon` 模式） | Strip 的逐顶点 UV 控制弱于 GPU 版；这是**跨族降级**，编译报告必须登记族变化 |
| 13 `vfx_surface_scatter` | `cpu_surface_scatter` | `Shape.MeshRenderer`（`Triangle` 模式，`useMeshMaterialIndex` 关）；`Main.simulationSpace = Local`；`Noise` 极弱 | SDF 源不可用（只能 mesh 源）；面积均匀性弱于 GPU 版 |
| 14 `vfx_mesh_shatter` | `cpu_mesh_shatter` | 降级为网格族的预破碎 + Rigidbody（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.17），块数按档截断。**跨族降级** | ML 档进一步降到"脚本/材质位移"（无刚体，`PROTOTYPE_CATALOG_v1.md` §2.4 的 ML 列） |

### 4.3 CPU 模块使用纪律

| 模块 | 允许 | 禁止 / 约束 |
|---|---|---|
| `Main` | 全部字段 | `useAutoRandomSeed` **必须** false（确定性截帧）；`playOnAwake` 必须 false（控制器所有权） |
| `Emission` | rateOverTime / rateOverDistance / bursts | — |
| `Shape` | Sphere / Hemisphere / Cone / Box / Circle / Edge / Mesh / MeshRenderer / SkinnedMeshRenderer | Mesh 源必须来自网格族生成器或宿主引用，不得引入外部模型 |
| `VelocityOverLifetime` / `LimitVelocityOverLifetime` / `ForceOverLifetime` | 全部 | — |
| `Noise` | 全部 | ML 档 `quality = Low` 且 `octaveCount = 1` |
| `ColorOverLifetime` / `ColorBySpeed` | 全部 | 卡通 / 像素风必须用 `Fixed` 模式梯度（§6.2） |
| `SizeOverLifetime` / `SizeBySpeed` | 全部 | — |
| `RotationOverLifetime` / `RotationBySpeed` | 全部 | 像素风量化到 90°（§6.3） |
| `Collision` | World（Planes / 3D / 2D） | `enableDynamicColliders` 默认关（避免对用户场景做假设）；`sendCollisionMessages` 仅在需要 decal 信号时开 |
| `SubEmitters` | Birth / Collision / Death，≤ 2 个 | 子系统计入同一层的峰值预算 |
| `Trails` | Particles / Ribbon | — |
| `Renderer` | Billboard / StretchedBillboard / HorizontalBillboard / VerticalBillboard / Mesh / None | `sortingLayerName` + `sortingOrder` 在 2D 下必须显式设置（§5.3） |
| **`TextureSheetAnimation`** | **禁止**（谓词 CP-5） | ADR-010 §5 素材纪律：这是 flipbook 的载体。粒子形状一律来自材质 SDF |
| `Lights` | **禁止** | 每粒子生成 Light 会绕过局部光预算（`PROTOTYPE_CATALOG_v1.md` §2.4 的"局部光数/预制体"）。局部光一律由 `local_light` 族显式声明 |
| `ExternalForces` | **禁止** | 依赖场景内 `ParticleSystemForceField`，违反"对外部零假设" |

### 4.4 CPU 峰值截断

`PROTOTYPE_CATALOG_v1.md` §2.4 的 CPU 粒子峰值：ML ≤ 60 / MM ≤ 150 / MH ≤ 300 / PL ≤ 400 / PM ≤ 600 / PH ≤ 1000（**预制体全部 CPU 层之和**）。编译器动作：

1. 计算每层的理论峰值 `peak = max(maxParticles, ceil(rateOverTime * lifetimeMax) + Σ bursts)`，含 SubEmitter。
2. 全层求和；超限时按层的 `priority`（原型层表的"必需/可选"+ recipe 顺序）从可选层开始按比例缩减 `rateOverTime` 与 `burstCount`，并把 `maxParticles` 截到实际峰值（避免 Unity 预分配浪费）。
3. 缩减后仍超限 → `E300` 编译错误并列出建议禁用的层。

**`maxParticles` 必须等于截断后的峰值**（不是留大余量）：谓词 CP-3 断言 `maxParticles` 与理论峰值的比值 ∈ [1.0, 1.25]。

---

## 5. 2D 排序：限制与应对

### 5.1 VFX Graph 在 URP 2D 下的限制（事实陈述）

1. **无 Sorting Layer 参与**：`VisualEffect` 使用的是 `MeshRenderer` 路径的排序语义，URP 2D Renderer 的 Sorting Layer / Order in Layer 分层不作用于它。因此一个 VisualEffect **无法**被插入到两个 `SpriteRenderer` 之间。
2. **整体深度排序**：整个 VFX 系统按其包围盒或变换深度参与透明排序，同一系统内的粒子相互之间按 GPU 输出顺序，不可控。
3. **不接收 Light2D**：URP 2D 的光照是 2D Renderer 的 pass，VFX Graph 的输出材质不在其中。因此 `light` 层（Light2D）照不亮 GPU 粒子。
4. **URP 2D Renderer 的 Renderer Feature 顺序**：VFX 输出通常落在所有 2D 光照层之后，表现为"始终在最上层或最下层"，取决于队列。

### 5.2 判定规则：何时必须用 CPU 粒子

编译器对 `dimension == 2d` 的 `gpu_particles` 层执行以下判定，**任一命中即强制降级为 CPU**：

| # | 条件 | 理由 |
|---|---|---|
| D1 | 该层的 `sorting.order` 落在其他非粒子层的 order 区间**内部**（即需要被夹在两层之间） | §5.1-1，做不到 |
| D2 | 该原型声明该层需要接收 2D 光照（层表中该层与 `light` 层有耦合语义，如 `surface` / `ground` 的被照亮） | §5.1-3 |
| D3 | 该层需要与 `SpriteRenderer` 类层做乘算混合（`_BLEND_MULTIPLY`，暗核语义） | 乘算对排序敏感，必须精确插序 |
| D4 | 该层需要逐粒子的碰撞回调驱动其他层（decal 信号） | GPU 事件回读到 C# 侧成本高且不确定 |
| 否则 | 允许 GPU | 典型：整体位于最前（`flash / emission.sparks`）或最后（`veil / body`）的层 |

判定结果进编译报告；被降级的层在报告中标注命中的条件编号。

### 5.3 CPU 粒子的 2D 排序设置

- `ParticleSystemRenderer.sortingLayerName` = `layers[].sorting.layer`（recipe 给出，缺省由编译器按原型层序分配）。
- `sortingOrder` = `layers[].sorting.order` + `TECH_FAMILY_SPEC_MATERIAL.md` §11 的类别偏移。
- `ParticleSystemRenderer.sortMode`：默认 `Distance` 在 2D 无意义，编译期改为 `OldestInFront`（发射顺序稳定，与 2D 的"后发射在前"直觉一致）。
- 整层用 `SortingGroup` 包裹的场合（一个层内含主系统 + SubEmitter）：`SortingGroup` 在层节点上，内部系统用相对 order。

### 5.4 3D 下的排序

3D 无此限制，GPU 粒子按 §2.2 的 Queue Offset 表（`TECH_FAMILY_SPEC_MATERIAL.md` §6.4）参与透明排序。

---

## 6. 像素风栅格吸附

`STYLE_CATALOG_v1.md` §3.3 要求粒子位置在渲染前吸附到虚拟栅格。以下给出**实现位置**与参数。

### 6.1 位置量化的实现位置

| 族 | 实现位置 | 具体 |
|---|---|---|
| GPU 粒子 | **VFX Graph 的 Output 块内，Orient 之前** | 在 Output Context 中插入一个 `Set Position` 操作：`position = floor(position / PixelWorldSize) * PixelWorldSize + PixelWorldSize * 0.5`。放在 Output 而不是 Update：Update 里量化会破坏力场积分（速度被反复吃掉），导致粒子卡住。**只量化渲染位置，不量化模拟位置** |
| CPU 粒子 | **粒子材质的顶点着色器内** | `mat_shape_sdf` 等粒子材质通过 `PixelSnapSize > 0` 启用一个顶点阶段的中心点吸附：取 quad 的中心（4 顶点的公共中心，由 `UNITY_PARTICLE_INSTANCE_DATA` 或 center-offset 重建），量化中心，再把 4 个角相对中心的偏移加回。**不能逐顶点量化**（会把 quad 撕成不规则四边形） |
| 网格粒子（两族通用） | 同上（网格粒子的中心即实例位置） | 网格自身的顶点由 `sg_vdisp_grid_snap` 另行量化（`TECH_FAMILY_SPEC_MESH_LIGHT.md` §5.4） |

### 6.2 参数

| 参数 | 类型 | 语义 | 来源 |
|---|---|---|---|
| `PixelWorldSize` | float | 一格的世界尺寸。2D 正交下 = `pixelSize`（风格参数）；3D 下 = `pixelSize * 参考距离`（由风格给基准） | `style.parameters.pixelSize` |
| `snapSpace` | enum | `worldXY`（2D 默认，多层天然对齐）/ `object`（3D 默认，体素感，ADR-010 §4bis-4）/ `parent` | `style.parameters.gridSpace` |
| `sizeQuantSteps` | int | 尺寸量化档数（像素风 4 档：1/2/3/4 格） | 风格约束 |
| `rotationQuantDeg` | float | 旋转量化角（像素 90°，卡通 0=不量化） | 风格约束 |

### 6.3 其他风格约束的落点

| 风格约束（`STYLE_CATALOG_v1.md`） | GPU 实现位置 | CPU 实现位置 |
|---|---|---|
| 基础形替换（圆→方 / 拉伸→短条） | Output 的材质变体（材质族 `sg_sdf_primitive_2d.shape`） | 同（材质决定形状，不是模块） |
| 尺寸量化 / 最小尺寸 | Initialize 块的 `StartSize` 后接量化节点 | `Main.startSize` 的曲线值在编译期离散化 + 材质内 `sizeQuantSteps` |
| 颜色 Fixed 模式（阶跃 2~5 色） | `ColorOverLife` 的 Gradient 在编译期转为阶跃梯度（相邻键时间差 < 1e-3） | `ColorOverLifetime.color` 设为 `GradientMode.Fixed` |
| 旋转量化 | Output 前 `angle = round(angle/q)*q` | 材质顶点阶段量化（`RotationOverLifetime` 无法量化） |
| Trail 宽度阶梯 + 末端硬截断 | `strip` 的 widthCurve 离散化 | `Trails.widthOverTrail` 曲线设为阶梯（`Constant` 切线） |
| 密度倍率（卡通 0.6 / 像素 0.5） | `SpawnRate` / `BurstCount` 乘倍率，**与档位上限取小**（`STYLE_CATALOG_v1.md` §5） | 同 |

---

## 7. 元素 → 模板速查表

| 元素 | GPU 模板（主） | 关键参数取向 | CPU 降级变体 | 输出形 |
|---|---|---|---|---|
| `fire` | 1 `vfx_buoyancy_turbulence` | `buoyancy` 中高，`turbStrength` 中，寿命 0.3~1.2 s，尺寸起大快缩（团）/ 起小恒定（星），触地弹跳后熄，寿命末 10% 分裂 | `cpu_buoyancy_turbulence` | `stretchedBillboard`（星）/ `quad`（团） |
| `ice` | 2 `vfx_gravity_settle` | 重力 1.0，湍流近 0，寿命 0.8~2 s，尺寸恒定后瞬消，触地滑动微弹**不消失**（停留后阈值消散），落地分裂 | `cpu_gravity_settle` | `meshParticle`（碎晶）+ `stretchedBillboard`（冰针） |
| `lightning` | 3 `vfx_instant_rephase` | `rephaseRate` 10~30，寿命 0.05~0.2 s，尺寸恒定，无碰撞，生成时分裂 2~3 | `cpu_instant_rephase` | `stretchedBillboard`（短） |
| `water` | 4 `vfx_gravity_drag_split` | 重力 1.0，轻阻力，拉伸比随速度，触地摊开成 decal，大滴分裂 3~6 | `cpu_gravity_drag_split` | `stretchedBillboard` / `meshParticle`（大滴） |
| `wind` | 5 `vfx_vortex` | 切向高、向心中、轴向上升，强湍流，长寿命，无碰撞 | `cpu_vortex` | `stretchedBillboard`（长丝）+ `quad`（卷起物） |
| `earth` | 2 `vfx_gravity_settle` | 重力 1.5，无湍流，弹跳 0.3 + 高摩擦 + 停留长，落地生尘（SubEmitter → 模板 9） | `cpu_gravity_settle` | `meshParticle`（碎石）+ `quad`（尘） |
| `poison` | 4 `vfx_gravity_drag_split` | 重力 0.3，强粘滞阻力，长寿命，泡起小变大后瞬消，泡破生 3~5 微滴 | `cpu_gravity_drag_split` | `quad`（泡/滴）+ 大 `quad`（毒雾） |
| `light` | 1 `vfx_buoyancy_turbulence`（湍流≈0） | 匀速上升，极弱侧摆，寿命中长，尺寸恒定或慢缩，无碰撞 | `cpu_buoyancy_turbulence` | `stretchedBillboard`（羽状）/ `quad` |
| `shadow` | 5 `vfx_vortex`（参数取负 = 向心） | 向心吸引 + 弱重力 + 中湍流，起大慢缩；**两个子系统**：黑片（`_BLEND_MULTIPLY`）+ 紫点（Additive） | `cpu_vortex` ×2 | `quad`（撕裂片）+ 点 |
| `arcane` | 6 `vfx_orbital_hover` | 轨道 + 悬浮 + 周期相位脉冲，长寿命，尺寸恒定 | `cpu_orbital_hover` | `meshParticle`（几何符号）+ 点 |
| `tech` | 7 `vfx_step_grid` | `stepRate` 中，`cellSize` = 栅格，可选曼哈顿路径，到格点短闪 | `cpu_step_grid` | `quad`（方）/ `meshParticle`（六边片） |
| `blood` | 4 `vfx_gravity_drag_split` | 重力 1.0，中阻力，拉伸比强于水，脉冲式 Burst（3~5 次间隔 0.05 s），触面成斑 | `cpu_gravity_drag_split` | `stretchedBillboard` + 点 |
| `nature` | 10 `vfx_fall_wind` | 弱重力 + 空气阻力 + 中湍流 + 翻飞旋转，长寿命，落地停留后淡 | `cpu_fall_wind` | `quad`（叶/瓣，SDF 形）+ 发光孢子点 |
| `none.dust` | 9 `vfx_drift_curl` | 大 quad，长寿命，起小变大，极慢，贴地时 Y 速度≈0 | `cpu_drift_curl` | `quad`（大而淡） |
| `none.rubble` | 2 `vfx_gravity_settle` | 强重力，弹跳滚动，停留，落地生尘 | `cpu_gravity_settle` | `meshParticle` |
| `none.liquid` | 4 `vfx_gravity_drag_split` | 同 `water`，无色（透明 + 高光） | `cpu_gravity_drag_split` | `stretchedBillboard` |
| `none.spark_metal` | 11 `vfx_burst_radial` | 直线短促，重力，弹跳 0.4，寿命 0.2~0.6 s，寿命末**硬切**（不渐隐） | `cpu_burst_radial` | `stretchedBillboard`（极短亮线） |
| 蓄力 / 拾取语义（跨元素） | 8 `vfx_attract_target` | 从壳层生成向中心汇聚，到达即消并发汇聚事件 | `cpu_attract_target` | 按元素 |
| 尾迹语义（跨元素） | 12 `vfx_strip_trail` | 宽度沿寿命衰减，UV 沿弧长 | 跨族降到 `TrailRenderer` | `strip` |
| 覆盖 / 侵蚀语义（跨元素） | 13 `vfx_surface_scatter` | 按面积散布，沿法线微飘，跟随宿主 | `cpu_surface_scatter` | `quad` |
| 破碎语义（跨元素） | 14 `vfx_mesh_shatter` | 从三角面生成网格粒子 | 跨族降到预破碎 + Rigidbody | `meshParticle` |

**REFERENCE_ANALYSIS §2-5 的落点**：「离散高亮点极少但极亮」——`emission.sparks` 类层的默认 `SpawnRate` / `BurstCount` 必须显著低于 `emission.smoke` / `body` 类层，且其材质的 `hot` 台阶倍率取上限。这不是建议，是谓词 GP-7 / CP-6 断言的：同一预制体内，标记 `sparkLike: true` 的层的峰值必须 ≤ 同预制体内 `body/smoke` 类粒子层峰值的 25%，且其 `_StopMultipliers.w`（hot 倍率）必须 ≥ 其他粒子层的 2 倍。

---

## 8. UGUI Overlay 伪粒子（material-based pseudo particles）

### 8.0 裁定回顾

ADR-010 §4bis-2：**双方案**——Screen Space-Camera / World Space 画布用真粒子；Overlay 画布自动降级为材质内程序化伪粒子（shader 多实例 SDF 点）。

### 8.1 为什么 Overlay 下真粒子不可用

`Screen Space - Overlay` 的 Canvas 由 `CanvasRenderer` 在所有相机之后单独绘制，`ParticleSystemRenderer` / `VisualEffect` 是常规 Renderer，无法进入这条绘制路径。可选的绕行（Canvas → RenderTexture 桥、额外相机）都会触碰"相机"这个资产外事项（`PROTOTYPE_CATALOG_v1.md` §2.0），因此排除。

### 8.2 伪粒子的实现规格

伪粒子是一个 `SG_VfxCanvas` 主图上的材质变体 `mat_ui_pseudo_particles`，在**一张 UGUI Graphic 的矩形内**用解析方式绘制 N 个运动中的 SDF 点。

**算法（逐像素，不是逐粒子）**：

```
// 常量：MAX_INSTANCES = 64（编译期上限，静态展开）
// 参数：instanceCount ∈ [1, 64]（≤ MAX_INSTANCES，多出的实例权重为 0）
accum = 0
for i in 0 .. MAX_INSTANCES-1:                 // 编译期完全展开的定长循环
    if i >= instanceCount: break               // 静态 unroll + 早退，非活跃实例零成本（编译期按 instanceCount 生成变体）
    h      = hash(i, _Seed)                    // 每实例的确定性随机四元组
    birth  = h.x * spawnSpread                 // 出生相位
    t      = frac((_LocalTime * rateHz + birth))   // 归一化寿命 ∈ [0,1)
    p0     = emitPoint + (h.yz * 2 - 1) * emitRadius
    v      = dirFromMode(motionMode, h, i)     // §8.3
    p      = p0 + v * t * speed + 0.5 * accel * t * t
    r      = sizeCurve(t) * baseSize
    d      = sdfShape(uv - p, shapeId, r, h.w * rotSpread)  // 复用 sg_sdf_primitive_2d
    a      = threshold(d) * fadeCurve(t)
    col    = paletteAt(t)                      // 复用 sg_comp_palette_lut
    accum += a * col                           // 加法累积
outColor = accum * _Intensity
```

**关键设计**：

- **定长展开而不是动态循环**：`instanceCount` 是 recipe 参数但**编译期烘死为静态关键字组**（`_INST_8 / _INST_16 / _INST_32 / _INST_64`，四挡）。逐像素的动态循环在移动端是灾难；四挡静态变体使成本可预测且进成本模型。
- **每实例的一切随机来自 `hash(i, _Seed)`**：保证确定性（截帧对比可复现）与零状态（无需模拟缓冲）。
- **形状复用材质族的 SDF 子图**：不新增形状实现，风格轴对形状的替换（圆→方）自动生效。
- **无碰撞、无子发射、无 Trail**：这三项在解析实现里成本不可控。需要它们的 F 类层必须使用 Camera/World Canvas 模式（编译期给出警告 `W4xx`）。

### 8.3 `motionMode` 闭集（与真粒子模板的对应）

| `motionMode` | 解析运动 | 对应的真粒子模板 |
|---|---|---|
| `rise` | `v = (jitterX, +1)`，加速度 0 | 1 `vfx_buoyancy_turbulence` |
| `fall` | `v = (jitterX * wind, -1)`，加速度 `-g` | 2 `vfx_gravity_settle` / 10 `vfx_fall_wind` |
| `radial` | `v = normalize(p0 - emitPoint)`，加速度 `-drag * v` | 11 `vfx_burst_radial` |
| `converge` | `p = lerp(p0, target, easeIn(t))` | 8 `vfx_attract_target` |
| `orbit` | `p = center + rot(h.x*2π + t*ω) * radius` | 6 `vfx_orbital_hover` |
| `drift` | `v` 由 `sg_noise_curl(p0, t)` 给 | 9 `vfx_drift_curl` |
| `flicker` | 位置每 `1/rate` 秒重采样（`floor(t*rate)` 进 hash） | 3 `vfx_instant_rephase` |

### 8.4 参数面对齐（同一 Recipe 字段两种实现）

这是本节的核心要求：**同一个 recipe 层在 Camera/World Canvas 下编译为真粒子、在 Overlay 下编译为伪粒子，recipe 文本一字不改**。对齐表：

| Recipe 参数（`layers[].parameters`） | 真粒子（CPU）落点 | 伪粒子落点 |
|---|---|---|
| `count` | `Emission.burstCount` 或 `rateOverTime * lifetime` | `instanceCount`（截断到 ≤ 64 并取最近的静态挡） |
| `lifetime` | `Main.startLifetime` | `1 / rateHz` |
| `speed` | `Main.startSpeed` | `speed` |
| `size` | `Main.startSize` | `baseSize` |
| `sizeOverLife` | `SizeOverLifetime.size` | `sizeCurve`（编译期采样为 4 点多项式） |
| `colorOverLife` | `ColorOverLifetime.color` | `paletteAt(t)`（编译期采样为 5 段） |
| `gravity` | `Main.gravityModifier` | `accel.y` |
| `emitShape` / `emitRadius` | `Shape` | `emitPoint` / `emitRadius`（UV 空间） |
| `motionMode` | 决定选哪个 CPU 变体（§4.2） | 决定 `motionMode` 关键字 |
| `shape` | 粒子材质的 `sdf_primitive_2d.shape` | 同一参数，同一子图 |
| `rotationSpread` | `Main.startRotation` 随机范围 | `rotSpread` |
| **不可对齐的项** | `Collision` / `SubEmitters` / `Trails` / `Noise.quality` | 无对应 → 编译期警告 `W402` 并忽略 |

**对齐由变体清单强制**：`cpu_*` 变体与 `mat_ui_pseudo_particles` 变体的清单必须声明相同的 `alignedParameterSet` 名称；谓词 UI-2 断言两者的参数键集合在该对齐集内完全一致。

### 8.5 成本档位

| `instanceCount` 挡 | 每像素 SDF 求值 | minTier | 备注 |
|---|---|---|---|
| 8 | 8 | ML | UI 面积通常小，可接受 |
| 16 | 16 | MM | |
| 32 | 32 | MH / PL | |
| 64 | 64 | PM / PH | UI 矩形面积应 ≤ 屏幕 15%，否则编译期警告 |

伪粒子层的 `sampleCost` 计为 `instanceCount / 8`（即 1~8），进材质族的采样预算（`TECH_FAMILY_SPEC_MATERIAL.md` §2.4）。

### 8.6 Canvas 模式的检测

- **编译期**：recipe 无 `canvasMode` 字段（T2a 未决问题 2 的 schema 影响，本文档裁定为**不加字段**）。编译器为 F 类的粒子层**同时**产出两种实现的能力：默认编译为真粒子（CPU），并在同一层节点上附加一个禁用的伪粒子 Graphic 子节点。
- **运行时**：预制体根上的 `VfxCanvasModeProbe` 组件在 `OnEnable` 时向上找 `Canvas.rootCanvas.renderMode`；为 `ScreenSpaceOverlay` 时禁用 ParticleSystem 子节点、启用伪粒子子节点，反之相反。
- **为什么运行时而不是编译期**：同一个预制体可能被用户放进任何 Canvas，`canvasMode` 不是 recipe 作者能知道的信息。这与折射的运行时检测（`TECH_FAMILY_SPEC_MATERIAL.md` §6.2）是同一纪律：**对外部零假设 = 检测 + 双路，不是拒绝**。
- **成本**：多一个禁用的子节点（约 3 个 GameObject + 1 材质）。计入 `VfxOutputAuditor` 的 GameObject 预算，F 类原型的预算表相应放宽（`COMPILER_BOUNDARY_V2.md` §4.2）。

---

## 9. 构造性谓词（EditMode 机器检查）

### 9.0 可断言性设计：模板清单

与材质族的子图清单同构，每个 VFX Graph 模板配 sidecar 清单：

```
Assets/VFX/Shared/VfxTemplates/<id>.vfx
Assets/VFX/Shared/VfxTemplates/<id>.vfxtemplate.json
   { "id", "guid", "field", "outputShapes": [...], "hasCollision", "hasEvents",
     "exposedProperties": [{name, type, min, max, default}], "dimensions": ["2d","3d"],
     "minTier", "capacityDefault", "cpuFallbackVariant", "sparkLike": bool }
```

VFX Graph 的图结构同样没有稳定公开 API；清单是断言面。`exposedProperties` 可以**双向校验**：`VisualEffectAsset` 的 `GetExposedProperties()` 是公开 API，谓词 GP-2 断言清单声明与资产实际暴露的属性集合完全一致——这使清单不能撒谎。

### 9.1 GPU 粒子族谓词（GP-*）

| 编号 | 谓词 |
|---|---|
| GP-1 | `Assets/VFX/Shared/VfxTemplates/` 下每个 `.vfx` 必有同名 `.vfxtemplate.json`；反之清单 `guid` 必须解析到存在的 `.vfx`。**无清单的模板不得存在** |
| GP-2 | 清单 `exposedProperties` 的名称与类型集合 == `VisualEffectAsset.GetExposedProperties()` 的实际集合（双向，无多无少）。这条使清单与资产不能漂移 |
| GP-3 | 每个模板必须暴露 §2.2 的全部 16 个统一属性（GP-2 的子集断言，单列以便错误信息精确） |
| GP-4 | 每个模板清单必须声明非空 `cpuFallbackVariant`，且该 id 存在于 CPU 变体清单（§4.2 的映射完备性——**没有降级目标的 GPU 模板不得入库**，因为 ML/MM 档必然需要它） |
| GP-5 | 产物中每个 `VisualEffect` 的 `visualEffectAsset` 必须是模板库成员（GUID 白名单）；`resetSeedOnPlay == false`；`startSeed` == recipe 的 `seed + layerOrdinal` |
| GP-6 | 模板清单的 `outputShapes` ⊆ §2.3 的 4 值闭集；清单新增布尔字段 `usesFlipbook` 必须全库为 false（ADR-010 §5） |
| GP-7 | 同一预制体内标记 `sparkLike: true` 的层的容量 ≤ 非 sparkLike 粒子层容量之和的 25%（REFERENCE_ANALYSIS §2-5） |
| GP-8 | 产物中不存在 `dimension == 2d` 且未通过 §5.2 判定的 `VisualEffect`（即：2D 下每个存活的 GPU 粒子层，编译报告必须记录它通过了 D1~D4 全部否定判定） |
| GP-9 | 产物中每个 `VisualEffect` 的 `Capacity` ≤ 该档位的 GPU 粒子上限（§2.4），且 ML/MM 档产物中 `VisualEffect` 组件数 == 0 |

### 9.2 CPU 粒子族谓词（CP-*）

| 编号 | 谓词 |
|---|---|
| CP-1 | 每个 `ParticleSystem` 的 `main.playOnAwake == false` 且 `main.useAutoRandomSeed == false` 且 `randomSeed == seed + layerOrdinal`（确定性截帧的前提） |
| CP-2 | 每个 `ParticleSystem` 至少启用一个**时间演化模块**：`colorOverLifetime` / `sizeOverLifetime` / `velocityOverLifetime` / `rotationOverLifetime` / `noise` / `trails` 中的任意一个。这是 ADR-009 EB-2 谓词在本族的对应物：静态点阵不构成粒子层 |
| CP-3 | `main.maxParticles` / 理论峰值 ∈ [1.0, 1.25]（§4.4） |
| CP-4 | 全部 `ParticleSystem`（含 SubEmitter）峰值之和 ≤ 该档位 CPU 上限 |
| CP-5 | **零个** `ParticleSystem` 启用 `textureSheetAnimation`（ADR-010 §5 禁序列帧）。这条是本族最重要的纪律谓词 |
| CP-6 | 零个 `ParticleSystem` 启用 `lights` 模块或 `externalForces` 模块（§4.3 禁止表） |
| CP-7 | `dimension == 2d` 时，每个 `ParticleSystemRenderer` 的 `sortingLayerID` 必须解析到工程内存在的 Sorting Layer，且 `sortingOrder` 与 §5.3 的偏移表一致 |
| CP-8 | 每个 `ParticleSystemRenderer` 的 `sharedMaterial` 的 shader 必须是材质族 4 张主图之一（`TECH_FAMILY_SPEC_MATERIAL.md` MV-6 的粒子侧延伸） |
| CP-9 | `renderMode == Mesh` 的渲染器，其 `mesh` 必须是产物目录内由网格族生成器产出的网格（不是 Unity 内置 Cube/Sphere 之外的导入模型；内置基础几何允许） |
| CP-10 | SubEmitter 数 ≤ 2 且 SubEmitter 自身不再有 SubEmitter（禁递归，成本可算） |

### 9.3 伪粒子谓词（UI-*）

| 编号 | 谓词 |
|---|---|
| UI-1 | 每个 F 类粒子层的层节点下**同时**存在一个 `ParticleSystem` 子节点与一个 `mat_ui_pseudo_particles` 的 Graphic 子节点，且二者初始 `activeSelf` 恰好一真一假 |
| UI-2 | 两个实现的参数键集合在其 `alignedParameterSet` 内完全一致（§8.4） |
| UI-3 | 伪粒子材质恰好启用一个 `_INST_*` 关键字，且 `instanceCount` ≤ 该关键字对应的上限 |
| UI-4 | 预制体根上存在 `VfxCanvasModeProbe`，且其引用的两个子节点非空 |
| UI-5 | 伪粒子材质的 shader 必须是 `SG_VfxCanvas`（不是其他主图） |

### 9.4 fail-closed 三路（本族具体化）

1. **未立法模板拒绝**：模板库出现清单里没有的 `.vfx` → FAIL（GP-1）。
2. **无变体声明拒绝**：`technique.variant` 不在 §3（GPU）或 §4.2（CPU）清单 → `E203`。
3. **资产不在白名单拒绝**：`VisualEffect.visualEffectAsset` 或粒子材质 shader 不在白名单 → GP-5 / CP-8 FAIL。

显式豁免机制与材质族同构（测试内常量表、豁免仍跑谓词、已消费集合恰等于声明清单）。

---

## 10. 审计清单

- [ ] 14 个 VFX Graph 模板全部有 id / 主力场 / 输出形 / 碰撞 / 事件 / 参数面 / 维度 / 成本档位 / 服务层角色与元素。
- [ ] 14 个 CPU 降级变体逐模板对照，含能力损失的诚实记录。
- [ ] 像素风栅格吸附给出三个族的实现位置（VFX Output 块 / 粒子材质顶点阶段 / 网格粒子中心）与参数。
- [ ] 2D 排序限制 4 条事实 + 4 条强制降级判定 + CPU 排序设置。
- [ ] UGUI Overlay 伪粒子：算法、7 种 motionMode、参数面对齐表、成本四挡、运行时检测机制。
- [ ] 构造性谓词 24 条（GP 9 / CP 10 / UI 5）+ fail-closed 三路 + 豁免机制。
- [ ] 无序列帧：CP-5 断言零个 `textureSheetAnimation`；GP-6 断言零个 `usesFlipbook`。
- [ ] 无资产外事项：CP-6 断言零 `lights` / `externalForces`；无相机、无后处理、无全局光。
- [ ] 正文无具体特效名。
