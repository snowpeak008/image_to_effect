# T2a 交付报告：原型目录 v1 / 元素目录 v1 / 风格目录 v1 / Recipe v2 Schema 草案

日期：2026-09-05
执行：T2a 设计子 agent（docs-only；只新增文件；无 git 写操作）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）
背景：`docs/plans/OPTIMIZATION_MASTER_PLAN.md` §8（T2a 卡：派发中 → 本报告提交验收）

---

## 0. 覆盖面声明（ADR-010 §10-2）

T2a 完成后三轴覆盖到：

| 轴 | 覆盖 | 目标 | 结论 |
|---|---|---|---|
| 原型轴 | **58 个原型**（A 战斗动作 13 · B 状态持续 10 · C 反馈事件 9 · D 物理破坏 8 · E 环境氛围 9 · F UI 内特效 9）+ G 编排级组合规则 1 节 | 40~60，覆盖 ADR-010 §3 七大类 | 达标；§3 骨架 34 个点名项全部有对应原型，另由穷举补出 16 个 |
| 元素轴 | **13 个元素**（火 / 冰 / 雷 / 水 / 风 / 土 / 毒 / 光 / 暗 / 奥术 / 科技 / 血 / 自然）+ 无元素 / 纯物理类（5 个物理子画像）+ 9 个后续候选 | ≥ 12 | 达标 |
| 风格轴 | **首批 2**（卡通 / 像素）+ 6 个后续候选一行定义 | 首批 2 | 达标；像素风按用户裁定走 shader 内量化、保持原分辨率、无低分辨率 RT |
| 维度轴 | 58 个原型每个给出 2D / 3D 投影差异；13 个元素每个给出 2D / 3D 变体差异；2 个风格每个给出 2D / 3D 差异 | 每个原型 | 达标 |

---

## 1. 产出清单

全部位于 `docs/design/paradigm/`（新建目录），UTF-8，中文正文 + 英文括注。

| 文件 | 内容 | 规模 |
|---|---|---|
| `PROTOTYPE_CATALOG_v1.md` | 覆盖面声明 / 穷举方法 / 排除项 / 共享层角色词表（21 角色）/ 标准节拍与事件 / 标准参数 / 六档基准预算 / 2D-3D 投影通则 / **分类总览表（用户审阅面）** / 58 原型逐节（职责 · 层结构 · 节拍 · 投影差异 · 接口 · 六档降级 · 元素敏感度）/ 编排级组合规则 / 审计清单 | 约 2400 行 |
| `ELEMENT_CATALOG_v1.md` | 覆盖面声明 / 元素定义与七段画像结构 / 元素强度轴 / **对接机制**（三层注入、ElementPreset 资产形状、无元素关系、多元素）/ 13 元素逐节 / 无元素·纯物理类 5 子画像与默认注入规则 / 元素 × 层角色敏感度总表 / 9 候选 / 审计清单 | 约 700 行 |
| `STYLE_CATALOG_v1.md` | 覆盖面声明 / 风格定义与"完全独立"的三处叠加机制 / StylePreset 资产形状 / 风格与元素的交界表 / 卡通（定义 · 材质族要求 · 粒子约束 · 几何约束 · 光约束 · 2D-3D · 张力）/ 像素（同结构，含用户裁定的 shader 内量化方案与"明确不做"）/ 6 候选 / 风格 × 档位交互 / 审计清单 | 约 250 行 |
| `RECIPE_V2_SCHEMA_DRAFT.md` | 覆盖面声明 / 设计原则 / 顶层结构 / 层 / 节拍与事件 / 接口与预制体映射 / 编排 / 六档如何影响编译 / **与 v1 不兼容不迁移的逐字段理由** / 2 个完整示例（3D 护盾 × 冰 × 卡通 × PC 中；2D 连锁 × 雷 × 像素 × 手机中）+ 1 编排片段（奥术）/ 错误码草案 / 未决问题 | 约 550 行 |
| `recipe-v2.schema.draft.json` | JSON Schema 2020-12 草稿：闭集枚举（dimension / 5 族 / 六档 / 阶段 / 58+1 原型 / 13+1 元素 / 风格）、层角色正则、编排条件约束、接口参数条件约束、标准参数禁止重声明 | — |
| `T2A_REPORT.md` | 本报告 | — |

**验证**（设计任务不跑构建 / 测试，但对 JSON 做了机器自检）：`recipe-v2.schema.draft.json` 通过 Draft 2020-12 元校验；文档内 3 个示例 recipe 全部通过 schema 校验（0 错误）；7 个故意破坏用例（编排携带 layers / 单原型携带 children / 重声明标准参数 `scale` / 非 none 元素携带 physicalProfile / 非法层角色 `sprite_sheet` / v1 字段 `camera_hints` / v1 档位 `pc_editor`）全部被拒绝（各 1 错误）。校验脚本为临时文件，已删除，未进仓。

`git status` 确认：仓内变更仅 `?? docs/design/`（新增目录），无既有文件被修改，无 git 写操作。

---

## 2. 穷举方法（如何保证覆盖面而不是拍脑袋列清单）

**按"画面职责"分类，不按外观。** 外观属于元素与风格轴；若按外观枚举会把三轴重新耦合。原型只回答"这个特效在画面里承担什么结构性职责"。

三个结构维度交叉：

- **空间形态**（6）：点 / 线 / 面 / 体 / 壳 / 多体
- **时间形态**（5）：瞬发 / 行进 / 持续 / 序列 / 衰减
- **附着主体**（5）：世界点 / 角色本体 / 挂点 / 两主体之间 / UGUI 矩形

6 × 5 × 5 = 150 格。剔除 Unity 不可表达格（如 UGUI 内体积雾）、与他格仅参数差异的格（合并规则：命中规则 / 发射规则 / 尺寸时长 / 正负面语义 / 形态"像什么" / 渲染取向都不构成新原型）、资产外事项格（后处理 / 相机 / UI 布局 / 全局光 / 时间缩放），并要求**"层结构 / 节拍 / 接口三者至少一项有实质差异"**才成为独立原型，得 58 个。每个原型节首行标注结构坐标（如 `S-P · T-T · H-W`），供审计"是否有两个原型坐标与层结构都相同"。

**明确排除项**已在原型目录 §2.0 列表（12 项），其中 2 项值得主 agent 注意：
- 折射 / 屏幕扭曲层：URP 内技术上属材质族（Scene Color 节点），但依赖用户 URP 资产开启 Opaque Texture，违反"对外部零假设"，v1 不作为任何层，全部用法线扰动高光 / UV 微扰替代（元素目录也据此约束水 / 火 / 冰）。
- 屏幕空间全屏过渡：属后处理 / UI 布局排除；世界空间的过渡幕（E07）是原型。

**层角色词表是三轴对接的枢轴**：21 个层角色（`core / body / edge / trail / emission / ground / decal / flash / shock / beam_column / link / mesh_shell / debris / surface / light / orbit / column / veil / cloth / fill / frame`，可带 `.后缀`）。原型用它声明层，元素预设按 (角色, 技术族) 索引注入，风格约束按角色类型叠加。"元素预设一次做好，所有原型自动获得"的机制就是这张词表 + 编译期合并顺序（原型默认 ← 元素 ← 风格约束 ← recipe 显式 ← 档位截断）。

---

## 3. 原型总数与分类分布

| 大类 | 数 | 原型 id |
|---|---|---|
| A 战斗动作 | 13 | projectile_linear · projectile_ballistic · projectile_homing · beam · melee_sweep · impact_burst · area_ground · strike_descending · wave_ring · chain_link · displacement · tether_link · summon_manifest |
| B 状态持续 | 10 | aura_ground · aura_body · orbitals · shield · charge_gather · channel_sustain · attach_marker · weapon_enchant · afflict_erode · restrain_bind |
| C 反馈事件 | 9 | hit_reaction · critical_emphasis · heal_restore · empower_rise · pickup_absorb · dissolve_out · entrance_in · parry_deflect · transform_shift |
| D 物理破坏 | 8 | fracture_burst · collapse_sequence · cloth_dynamic · liquid_splash · liquid_stream · surface_decal · soft_deform · ground_rupture |
| E 环境氛围 | 9 | weather_precip · ambient_airflow · fog_volume · surface_field · vent_emitter · portal_gate · sweep_curtain · barrier_wall · light_body |
| F UI 内特效 | 9 | ui_button_feedback · ui_shine_loop · ui_reward_burst · ui_reveal · ui_merge_fuse · ui_hud_pulse · ui_bar_energy · ui_collect_fly · ui_panel_enter |
| G 编排级 | 规则 | orchestration（≤ 8 子实例、同维度同风格同档位、不嵌套、`anchor` / `timeline` / `wiring` / 聚合接口 / 按 `priority` 降级） |
| **合计** | **58** | |

ADR-010 §3 骨架 34 个点名项全部落到原型；穷举额外补出 16 个（A09 扩散环、A12 牵引链接、B03 环绕体、B06 引导施法、C08 格挡弹反、C09 形态切换、D07 弹性形变、D08 地面裂陷、E02 空气粒子流、E05 喷发源、E08 屏障墙、E09 光源体、F02 / F07 / F08 / F09）。

**每原型均含**：职责一句话 · 层结构表（层名 / 职责 / 可选技术族标 ★ 首选 / 必需或可选）· 节拍与事件 · 2D / 3D 投影差异 · 特有参数与事件 · 六档降级表（每层 × 6 档）· 元素敏感度。

**局部光纪律**：凡有发光语义的原型 `light` 层为必需；两处有理由的例外已在节内注明（B07 挂点标记：多实例并存；F 类：UGUI 不受 Light2D 影响，由材质发光承担）。低档策略一律是"光烘进材质发光"而不是删掉职责。

---

## 4. 元素与风格摘要

**元素（13 + none）**：每元素七段——物理画像（运动 / 形态 / 边缘 / 能量 / 重力感 / 残留）、5 色 HDR 线性色板（primary / secondary / hot / cool / residue）、材质族预设（噪声形态 · 层数速度比 · 阈值性格 · 边缘 · 流动 · 顶点位移 · 发光）、GPU 粒子预设（主力场 · 基础形 · 寿命尺寸 · 碰撞 · 事件）、CPU 粒子预设（模块组合翻译）、几何族预设（基础几何族 · 生成器 · 破碎风格）、局部光预设（色温 · 倍率 · 闪烁模式 · 衰减）；随后是层角色注入表与 2D / 3D 差异。元素 × 层角色敏感度总表（H/M/L/—）给 T2b 排实现优先级。无元素类含 5 个物理子画像（dust / rubble / cloth / liquid / spark_metal）与默认注入规则，保证任何原型无元素时也能编译出"结构可见"的中性预制体（画廊用它验收结构）。

**风格（2 + 6 候选）**：风格独立性靠三处叠加点实现且零处结构改动——① 材质末端固定 `StyleStage` 子图插槽（`Style_Cartoon / Style_Pixel / Style_None`）；② 粒子 / 几何"形状与颗粒度"约束参数集（优先级高于元素、低于 recipe）；③ 局部光约束（强度分档 / 闪烁量化 / 阴影许可）。卡通 = 色阶分层 + 描边（3D 法线外扩壳，2D SDF 描边）+ 锐利阈值 + 高饱和。像素 = shader 内 UV / 世界坐标栅格量化 + 有限色带 + alpha 二值化与 Bayer 抖动 + 时间量化（≠ 序列帧，形态仍程序化）+ 粒子 / 几何吸附栅格；**明确不做**低分辩率 RT、相机 / 后处理 pass、序列帧。

---

## 5. 对 T2b 的输入建议

### 5.1 技术族实现规格（T2b 主体）

1. **Shader Graph 子图库**：按元素目录各元素"材质族预设"中出现的形态子图列清单——至少：`noise_fbm_2layer`（可配拉伸方向 / 速度比）、`noise_voronoi_crystal`、`noise_jagged_1d`（雷折线）、`sdf_shapes`（圆 / 环 / 星 / 多边形 / 扇 / 圆角矩形 / 叶形）、`sdf_sacred_pattern`（光几何阵）、`sdf_rune_ring`（奥术）、`sdf_hex_grid / scanline`（科技）、`sdf_crack_branch`（裂纹 / Lichtenberg）、`threshold_soft / hard / grow`（阈值三型，含"从种子点沿 Voronoi 蔓延"）、`fresnel_edge`、`polar_uv`、`vertex_displace_*`（舌形拉伸 / 鼓泡 / 摆动 / 隆起 / 张力抖动 / Gerstner）、`smoothmin_union`（液体）、`multiply_dark_core`（暗）、`StyleStage` 插槽 + `Style_Cartoon / Style_Pixel / Style_None`。
2. **VFX Graph 模板**：按元素"GPU 粒子预设"主力场归类——`field_buoyancy_turbulence`（火）、`field_gravity_settle`（冰 / 土 / 血）、`field_instant_rephase`（雷）、`field_gravity_drag_split`（水 / 毒）、`field_vortex`（风）、`field_orbital_hover`（奥术）、`field_step_grid`（科技）、`field_attract_target`（蓄力 / 拾取）、`field_drift_curl`（空气粒子）、`field_fall_wind`（天气）；输出形：拉伸 billboard / quad / 网格粒子；碰撞：平面 / SDF；事件分裂。像素风需要 Output 前位置吸附栅格的块。
3. **程序化网格生成器**（几何族）：`crystal_cluster`、`rock_chunk`、`jagged_polyline`（递归分叉）、`spiral_ribbon`、`tendril_blob`、`wire_polyhedron`、`tech_panel`、`vine_spline`、`branch_tree`、`sweep_band`（近战扫掠面）、`catenary_band`（牵引链）、`parabola_tube`（液体流）、`splash_crown`、`shell_polyhedron`、`ring_torus_segments`、`cylinder_beam`、`voronoi_prefracture`（2D / 3D 预破碎）、`subdivided_plane`（表面场 / 布料）。
4. **局部光节拍器**：统一组件，参数面 `{ color, intensity, range, flickerMode(steady/breathe/flicker/pulse/strobe), flickerRate, flickerDepth, decayShape(exp/linear/step/smooth/spike), castShadows }`，支持风格量化（`intensitySteps` / `flickerQuantize` / `frameRate`），3D 驱动 `Light`，2D 驱动 `Light2D`，`光:烘` 档退化为向材质写发光倍率。
5. **元素预设资产**（ElementPreset）与**风格预设资产**（StylePreset）的序列化格式，形状见元素目录 §2.2 与风格目录 §1.3。
6. **变体库清单**（`technique.variant`）：本草案示例用的变体名是占位语义名；T2b 需给每族一份变体清单（id / 支持维度 / 参数面 / 成本估算 / 构造性谓词）。

### 5.2 编译器边界修订

- 识别组件从 `ParticleSystem / TrailRenderer` 扩到 `VisualEffect / MeshRenderer+MeshFilter / SpriteRenderer / Light / Light2D / Rigidbody(2D) / Cloth / LineRenderer / Decal Projector`；shader / VFX 资产准入白名单 = 子图库 + 模板库。
- 成本模型六档：原型目录 §2.4 基准预算表为起点（GPU 粒子 / CPU 粒子 / 材质噪声层 / 局部光数 / 破碎块 / Cloth 档 / 透明叠加层 / 网格顶点）。
- 运行时控制组件 v2：任意阶段集合、`sustain` 循环、原型特有入事件分派、出事件（C# event + UnityEvent）、参数块与绑定表、外部引用为空时的退化规则。现有 `GeneratedVfxController` 三阶段结构可作雏形参考，但不复用。
- 门禁：ADR-009 方法论按技术族构造性谓词继承（草案 §7-6 列了 3 条样例），T2b 出清单。

### 5.3 画廊场景

- 2D / 3D 两个独立场景（正交 / 透视相机、Sorting Layer / 光照环境），九宫格翻页，Play 即检。
- 建议画廊格子的**默认填充**是 `element: none` 中性预制体（结构可见），再叠元素与风格——这样结构缺陷与元素 / 风格缺陷可以分开看。

### 5.4 T2c 范式样片建议（本报告是唯一允许出现具体样片名的位置之一，ADR-010 §10-1）

3 原型 × 3 元素 × 1 风格，2D / 3D 各一组。建议选择**跨大类、跨技术族主角、跨元素性格**的组合，避免全是"发光能量类"：

| 原型 | 主角技术族 | 理由 |
|---|---|---|
| B04 `shield` 护盾 | 网格几何 + 材质 + 局部光 | 壳体 / 涟漪 / 裂纹 / 破碎全链路，检验几何族与接口（`hitAt / integrity / break`） |
| A10 `chain_link` 连锁 | 程序化折线网格 + 材质 + 多盏局部光 | 检验非 billboard 形态与事件序列（`onHop`） |
| C06 `dissolve_out` 消散 | 材质阈值 + 粒子从阈值边缘发射 | 检验"作用于目标渲染器"与元素形态表现力（消散方式是元素差异最大的场景） |

| 元素 | 理由 |
|---|---|
| `ice` 冰 | 硬边 / 晶格 / 直落——与火性格相反 |
| `lightning` 雷 | 跳变 / 分叉 / 频闪——检验时间量化与局部光 strobe |
| `poison` 毒 | 粘滞 / 鼓泡 / 缓脉——检验软形态与慢节拍 |

风格：首批建议 `cartoon`（描边壳与色阶对三原型都有明确表现；像素风留第二批，因其 3D 读感是"体素感"，首次范式判定用卡通更不易被维度差异干扰）。

九格 = {shield, chain_link, dissolve_out} × {ice, lightning, poison} × cartoon，2D / 3D 各一组共 18 个预制体。

---

## 6. 未决问题清单（需主 agent / 用户拍板）

| # | 问题 | 影响 | 建议 |
|---|---|---|---|
| 1 | **折射 / 屏幕扭曲层**：URP Scene Color 依赖用户开启 Opaque Texture，违反"零假设"。v1 全部用法线扰动高光 / UV 微扰替代。是否接受"永不做折射"，或允许作为"检测到 Opaque Texture 可用时才启用的可选扩展层"？ | 水 / 冰 / 火热浪的真实感上限 | 建议 v1 不做，T3 后按用户反馈评估可选扩展 |
| 2 | **UGUI Overlay Canvas 下的粒子**：ParticleSystem / VFX Graph 不能直接渲染进 Screen Space-Overlay Canvas。选项：(a) F 类只支持 Screen Space-Camera / World Space Canvas；(b) 提供 Canvas RenderTexture 桥（触碰相机，倾向违反资产外纪律）；(c) F 类粒子层用 UGUI Graphic 材质的"程序化伪粒子"（shader 内多实例 SDF 点）替代真粒子 | F 类 9 个原型的粒子层可行性；Recipe `dimension` 是否需第三值 `ui` 或 `canvasMode` 字段 | 建议 (a) + (c) 组合：Camera / World 模式用真粒子，Overlay 模式降级为材质伪粒子；T2b 验证后定 schema 字段 |
| 3 | **编排 `recipeRef` 语义**：引用 recipe id（编译时内联，六档一致、确定性强）还是引用已编译预制体（链接，复用快但档位 / 版本可能错位）？ | 编排编译流程与门禁 | 建议内联 |
| 4 | **像素风 3D 网格层栅格空间**：物体空间（旋转时像素跟随物体，无爬行但透视下不对齐屏幕）vs 屏幕空间（对齐但移动时爬行）。默认物体空间；是否接受 3D 像素风的"体素感"定位？ | 像素风在 3D 的读感 | 建议接受体素感，2D 为像素风主场 |
| 5 | **描边壳成本**：卡通 3D 描边壳每网格层 +1 draw call，ML / MM 禁用改材质暗边。是否接受"低端档卡通无真描边"？ | 手机低 / 中卡通观感 | 建议接受，T2b 在画廊里对比 |
| 6 | **A07 地面范围的三段节拍**是否需要拆成两个原型（预警 / 爆发）以便单独复用？本目录合并为一个（`telegraphDuration` 可为 0） | 原型数 58 → 59 | 建议保持合并 |
| 7 | **`attach_marker`（B07）允许无局部光**、**F 类无局部光**——两处对"局部光一等成员"纪律的例外是否认可？ | 纪律一致性 | 建议认可（理由已在节内） |
| 8 | **元素强度轴 `element.intensity`** 与标准参数 `intensity` 命名易混。是否改名（如 `elementStrength` / `flavor`）？ | Schema 字段名 | 建议改为 `element.strength`，T2b 定稿时一并改 |
| 9 | **58 个原型的层数总和约 300 层**，六档 × 2 维度 × 13 元素 × 2 风格的组合空间巨大。T3 / T4 铺开顺序建议按元素目录 §5 敏感度总表的 H 格优先，且先做 2D 还是 3D 需用户定 | 铺开节奏 | 建议 3D 先行（几何族与局部光在 3D 表现力更完整，2D 投影是其子集），画廊两者同步 |
| 10 | **多元素**：v1 只做按层覆写（`elementOverride`），不做混合插值。熔岩 / 蒸汽等"固定组合元素"是否需要在 T4 单列为元素？ | 元素目录扩充 | 建议 T4 视样片反馈决定 |

---

## 7. 纪律自检（ADR-010 §10）

| 红线 | 结果 |
|---|---|
| 例子不进正文 | 原型 / 元素 / 风格目录正文无具体特效名；具体组合只出现在 Recipe v2 草案 §9 示例与本报告 §5.4 样片建议。已 grep 复核："元素名 + 形体名"式特效命名在三份目录中零出现；序列帧相关词仅在排除项与审计清单中以"禁止"语义出现 |
| 覆盖面声明 | 5 份文档开头均有 |
| 资产外事项不进原型 / 层 | 排除项 12 条明列；C02 `freezeFrames` 显式限定为"预制体内部各层保持峰值帧数，不碰时间缩放"；F06 屏幕边缘红晕限定为"用户放置的 RectTransform 上的 Graphic 材质" |
| 无序列帧 | 全部形态程序化；像素风时间量化已说明 ≠ 序列帧；v1 `atlas_id / atlas_fps / snap_fps / virtual_res` 语义在 v2 删除 |
| 技术族 5 族 | Schema `techniqueFamily` 闭集；无外部工具 |
| 只新增文件 | `git status` 仅 `?? docs/design/` |
| 无 git 写操作 | 未 add / commit / push |
| 不跑构建 / 测试 | 仅对 JSON Schema 与示例做了本地机器自检（临时脚本已删） |

---

## 8. 建议的验收动作（主 agent）

1. 用户审 `PROTOTYPE_CATALOG_v1.md` §3 分类总览表与数量（58）；对 §6 未决问题 1~5 拍板。
2. 主 agent 审三份目录的层角色词表一致性（原型 §2.1 ↔ 元素 §5 总表 ↔ schema `role` 正则）与排除项。
3. 通过后提交，并在 `OPTIMIZATION_MASTER_PLAN.md` §8 将 T2a 置 DONE、T2b 解锁；ADR-010 §3 的"预计 40~60"更新为"v1 = 58"。
