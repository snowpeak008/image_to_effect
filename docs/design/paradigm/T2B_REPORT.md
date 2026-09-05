# T2b 交付报告：5 技术族实现规格 / 卡通·像素材质族设计 / 编译器边界 v2 / 2D·3D 画廊规格

日期：2026-09-05
执行：T2b 设计子 agent（docs-only；只新增文件；无 git 写操作；不跑构建 / 测试）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`，含 §4bis 八条用户裁定）
质感标尺：`docs/design/references/REFERENCE_ANALYSIS.md`（§2 六条质量准则 + §3bis 辉光参数面）
输入：`T2A_REPORT.md` §5 对 T2b 的输入建议、`PROTOTYPE_CATALOG_v1.md`、`ELEMENT_CATALOG_v1.md`、`STYLE_CATALOG_v1.md`、`RECIPE_V2_SCHEMA_DRAFT.md` + `recipe-v2.schema.draft.json`
**验收状态：`ACCEPTED`（2026-09-05）**——主 agent 初审通过（覆盖对照 / 绑定 ~112 键论证 / 三件套不修订结论）；用户自行合入 `1168e2c6`；用户拍板未决见文末 §9。代码实现另走 worktree 线，交付报告命名 `T2B_IMPL_REPORT.md` 以免与本规格报告撞名。

---

## 0. 覆盖面声明（ADR-010 §10-2）

T2b 完成后：

| 项 | 目标 | 达成 |
|---|---|---|
| **5 技术族全部有实现规格** | 5 / 5 | 达标。材质族（`TECH_FAMILY_SPEC_MATERIAL.md`）、GPU 粒子族 + CPU 粒子族（`TECH_FAMILY_SPEC_PARTICLES.md`）、网格几何族 + 局部光族（`TECH_FAMILY_SPEC_MESH_LIGHT.md`） |
| **变体清单覆盖 58 原型的全部层角色需求（21 角色）** | 21 / 21 | 达标，逐角色对照见 §3 |
| **风格轴覆盖首批 2** | 2 / 2 | 达标。卡通与像素的 `StyleStage` 子图内部实现、5 族约束落点、StylePreset 默认值表（含辉光 18 参数）、风格 × 六档交互（`STYLE_IMPL_CARTOON_PIXEL.md`） |
| **元素轴 13 个的五族预设有可实现的落地形式** | 13 + `none` 的 5 子画像 | 达标。四份技术族规格各含一张"元素 → 子图/模板/生成器/光预设"速查表，逐元素给出关键参数取向 |
| **六档全部有降级机制规格** | 6 / 6 | 达标。材质子图替换表 + 辉光截断 + `材:静` 语义；GPU→CPU 逐模板降级映射 + CPU 峰值截断；生成器逐个六档顶点预算 + 破碎块六档 + Cloth 六档；局部光六档含 `光:烘` 的四步机制；编译器侧的八项成本函数 + 六档上限表 + 确定性降级顺序 |
| **2D / 3D 画廊各一** | 2 / 2 | 达标（`GALLERY_SPEC.md`） |

---

## 1. 产出清单

全部新增到 `docs/design/paradigm/`，UTF-8，中文正文 + 英文括注。**无既有文件被修改，无 git 写操作。**

| 文件 | 内容 | 规模 |
|---|---|---|
| `TECH_FAMILY_SPEC_MATERIAL.md` | 覆盖面声明 / 职责与产物形状 / **4 张主图闭集** / **53 个子图**（噪声 7 · SDF 9 · 阈值 5 · 边缘 5 · 流动 7 · 顶点位移 8 · 合成 9+直通 4 · 风格插槽 3）/ `StyleStage` 9 端口契约 / **辉光层 8 参数逐个实现 + 18 项参数面** / **折射双路 + 运行时检测 + 三方案裁定 + Queue Offset 表** / HDR-线性纪律与强度台阶的数值化谓词 / **25 个材质变体** / 元素→子图组合速查（18 行）/ 六档降级三旋钮 / 2D 排序表 / **31 条构造性谓词** | 约 740 行 |
| `TECH_FAMILY_SPEC_PARTICLES.md` | 覆盖面声明 / 两族分工与编译期选族规则 / VFX Graph 模板结构契约与 16 项统一属性 / **14 个 GPU 模板** / **14 个 CPU 降级变体逐一对照（含能力损失的诚实记录）** / 模块允许-禁止表（禁 `TextureSheetAnimation` / `Lights` / `ExternalForces`）/ CPU 峰值截断 / **2D 排序 4 条限制 + 4 条强制降级判定** / **像素栅格吸附三处实现位置** / **UGUI Overlay 伪粒子完整规格**（算法 / 7 种 motionMode / 参数面对齐表 / 四挡成本 / 运行时检测）/ 元素→模板速查（22 行）/ **24 条构造性谓词** | 约 400 行 |
| `TECH_FAMILY_SPEC_MESH_LIGHT.md` | 覆盖面声明 / 网格族职责与"编译期生成"裁定 / 生成器统一契约（uv1 与 colors 的接口约定）/ **20 个生成器逐个规格**（含 ADR-010 §4bis-8 新增的 `radial_spike_array` 与补齐的 `splash_crown`）+ 2 个运行时几何组件 + 描边壳 / **预破碎与 Rigidbody 接线**（层次 / 初始状态 / 触发 / layer 纪律 / 休眠回收 / ML 无刚体路径 / 2D）/ **Cloth 六档 + 对外部零假设的碰撞体策略** / **顶点位移与材质族的分工边界（3 个定义 + 7 条判定规则 + 层角色推论表）** / **局部光节拍器组件规格**（20 项参数面 / 5 种波形 / 无反射的双驱动 / `光:烘` 四步 / `flickerCoupling` 的 MPB 广播同步 / 元素速查 / 风格交互 / 档位表）/ 元素→生成器速查 / **30 条构造性谓词** | 约 480 行 |
| `STYLE_IMPL_CARTOON_PIXEL.md` | 覆盖面声明 / 三处叠加点的编译时刻与写入面 / **卡通**（色阶分层 + 3 条色带对策 / 锐利阈值的编译期注入 / 描边三路含 ADR-010 §4bis-5 低端档暗边 / 高饱和 / HDR 压台阶 / 内描线 / **与「硬柔并存」的 4 条兼容措施** / 5 族约束落点 / 2D-3D 差异）/ **像素**（坐标量化在上游+颜色量化在下游的两半结构 / 4 种栅格空间与 **3D 体素感三件套** / 有限色带 / alpha 二值化 + Bayer 抖动 / **时间量化 ≠ 序列帧的 6 条对照** / 6 处栅格吸附 / 5 族约束落点 / 与 §2 六准则的兼容含 `veil` 层救济 / 2D-3D 差异）/ **风格不覆盖元素的边界表（16 行）** / **StylePreset 默认值表（material 20 项 + 辉光 18 项 + 粒子/网格/光 21 项）** / **风格 × 六档交互表** / **24 条构造性谓词** | 约 380 行 |
| `COMPILER_BOUNDARY_V2.md` | 覆盖面声明 / **v1 边界 10 条事实核实与 v2 处置** / **识别组件闭集 18 类 + 6 个运行时脚本 + 排除表** / **资产准入白名单三方案对比与裁定（GUID 直接依赖 + 路径根传递闭包）** + 清单形状 + 双向真实性 / **八项可机器计算的成本函数 + 六档上限表 + 新增分项结构预算 + `C_over` 估计法与诚实边界 + 确定性降级顺序** / **控制器 v2 规格**（任意阶段集合 / `sustain` 循环与安全阀 / 入事件二分分派 / 出事件双形式 struct payload / 池化复位 9 项 / 外部引用为空的 6 条退化 / 参数块与绑定表 / 三种暴露方式 / 编排内联展开）/ **绑定 allow-list v2**（三段键 · 分族表 · **总键数 ~112 且不随内容增长**的论证 · 编译期解析六步）/ **门禁谓词 156 条汇总 + fail-closed 三路 + 显式豁免 5 条** / **三件套写入面：不修订的结论与三条理由** / 折射的编译期处理与报告声明格式（含 RF-1~5）/ 14 个新增错误码 | 约 480 行 |
| `GALLERY_SPEC.md` | 覆盖面声明 / 两个独立场景的完整配置 + 3D 不放平行光的理由 / 九宫格布局与格的四个组成 + 2D 格间排序隔离 / **翻页四方案对比与"键盘+UI+可开关轮播"裁定** + 完整交互清单 + 页数据源资产形状 / **Bloom 可开关**（场景 Volume + 开关脚本 / 默认开启及理由 / 状态写入截帧 profile / Tonemapping 不做开关的理由 / 判据的机器化）/ **格子填充与页组织三方案对比与矩阵页裁定** + 7 个默认页 / **截帧对比**（复用 `W24ContinuousCaptureRecorder` 的 7 项能力与具体接线 / 两种模式分工 / 截帧前 6 项强制状态 / 7 个节拍帧 × 双 Bloom pass / 输出目录与命名 / **对比图脚本生成方案 + 4 个辅助指标**）/ 画廊资产边界与三种混入方式的机器检查 / **10 条构造性谓词** | 约 300 行 |
| `T2B_REPORT.md` | 本报告 | — |

**验证**（设计任务不跑构建 / 测试）：本卡产出全部为文档，无 JSON / 代码可机器自检。文档内的所有清单计数经 grep 复核：子图 53、材质变体 25、GPU 模板 14、CPU 变体 14、生成器 20、**构造性谓词 156 条（编号唯一，无重号）**。

---

## 2. 变体清单总数

| 族 | 变体 / 模板 / 生成器 | 数 | 命名规则 |
|---|---|---|---|
| 材质 `material` | 主图 4 + 子图 53 + **变体 25** | 25 | `mat_<形态族>_<性格>` |
| GPU 粒子 `gpu_particles` | **模板 14** | 14 | `vfx_<主力场语义>` |
| CPU 粒子 `cpu_particles` | **变体 14**（与 GPU 一一对应） | 14 | `cpu_<主力场语义>` |
| 网格 `mesh` | **生成器 20** + 运行时组件 2 + 描边壳 1 | 23 | `<形态名词>`（如 `crystal_cluster`） |
| 局部光 `local_light` | **组件 1**（`VfxLightBeat`）× 2 驱动（`Light` / `Light2D`）× 5 波形 × 5 衰减形 | 1 组件 / 2 变体 id（`point_beat` / `light2d_point_beat`） | `<载体>_beat` |
| **合计** | | **78 个变体 id** | 全部 `^[a-z][a-z0-9_]*$`，**禁止在变体名中出现元素名或原型名** |

全部变体名均不含元素名与原型名——变体是**形态能力**，元素与原型通过参数注入。这是 ADR-010 §10-1「例子不进正文」在资产命名层的延伸。

---

## 3. 覆盖对照：21 层角色 × 5 族

`✔` = 该组合有明确的变体/模板/生成器可用；`—` = 该组合在设计上不成立（不是遗漏）；括注给出主要承担者。

| # | 层角色 | material | gpu_particles | cpu_particles | mesh | local_light |
|---|---|---|---|---|---|---|
| 1 | `core` | ✔ `mat_volume_fbm` / `mat_crystal_cell` / `mat_liquid_blob` / `mat_shape_sdf` | ✔ 1,5,6（稠密粒子核） | ✔ 同 | ✔ `crystal_cluster` / `rock_chunk` / `tendril_blob` / `cylinder_beam` / `splash_crown` | — |
| 2 | `body` | ✔ `mat_volume_fbm` / `mat_volume_march` / `mat_veil_soft` | ✔ 9,5,13 | ✔ 同 | ✔ `shell_polyhedron` / `spiral_ribbon` | — |
| 3 | `edge` | ✔ `mat_shell_fresnel` / `mat_jagged_band` / `mat_radial_spike` | ✔ 3（电弧边） | ✔ 同 | ✔ `ring_torus_segments` | — |
| 4 | `trail` | ✔ `mat_volume_fbm`（带材质） | ✔ 12 `vfx_strip_trail` / 1 / 2 | ✔ `cpu_strip_trail`（跨族降到 TrailRenderer） | ✔ `trail_mesh` / `sweep_band` / `spiral_ribbon` | — |
| 5 | `emission.*` | ✔ 粒子材质（`mat_shape_sdf` / `mat_leaf_petal`）+ **`mat_ui_pseudo_particles`**（Overlay） | ✔ **全部 14 个模板的主场** | ✔ 全部 14 个 CPU 变体 | ✔ `radial_spike_array`（几何式放射） | — |
| 6 | `ground` | ✔ `mat_ring_polar` / `mat_sacred_pattern` / `mat_rune_ring` / `mat_crack_grow` | ✔ 13 `vfx_surface_scatter` | ✔ 同 | ✔ `subdivided_plane` / `branch_tree` / `ring_torus_segments` | — |
| 7 | `decal` | ✔ `mat_crack_grow` / `mat_liquid_blob` / `mat_bubble_surface` | — | — | ✔ `subdivided_plane`（贴地）；`DecalProjector` 可选 | — |
| 8 | `flash` | ✔ `mat_shape_sdf` / `mat_radial_spike` / `mat_sacred_pattern` | ✔ 11 `vfx_burst_radial` | ✔ 同 | ✔ `radial_spike_array` | ✔（`flash` 常与 `light` 同拍） |
| 9 | `shock` | ✔ `mat_ring_polar` / `mat_radial_spike` | ✔ 11 / 5 | ✔ 同 | ✔ `ring_torus_segments` / `sweep_band` / `splash_crown` | — |
| 10 | `beam_column` | ✔ `mat_jagged_band` / `mat_volume_fbm` | ✔ 12 | ✔ 同 | ✔ `cylinder_beam` / `parabola_tube` | — |
| 11 | `link` | ✔ `mat_jagged_band` / `mat_liquid_blob` | ✔ 3 / 12 | ✔ 同 | ✔ `jagged_polyline` / `catenary_band` / `vine_spline` / `line_mesh` / `parabola_tube` | — |
| 12 | `mesh_shell` | ✔ `mat_shell_fresnel` / `mat_shell_ripple` / `mat_grid_cells` / `mat_dissolve_edge` | ✔ 13 / 14 | ✔ `cpu_surface_scatter` | ✔ `shell_polyhedron` / `wire_polyhedron` / `tech_panel` / `crystal_cluster` | — |
| 13 | `debris` | ✔ `mat_crystal_cell` / `mat_dissolve_edge`（断面 `colors.a`） | ✔ 14 `vfx_mesh_shatter` / 2 | ✔ `cpu_gravity_settle`（Mesh 渲染） | ✔ **`voronoi_prefracture`** / `rock_chunk` / `tech_panel` | — |
| 14 | `surface` | ✔ `mat_surface_wave` / `mat_bubble_surface` / `mat_grid_cells` | ✔ 13 | ✔ 同 | ✔ `subdivided_plane` | — |
| 15 | `light` | ✔（`光:烘` 档的烘焙发光项） | — | — | — | ✔ **`VfxLightBeat`（本角色的唯一承担者）** |
| 16 | `orbit` | ✔ `mat_rune_ring` / `mat_shape_sdf` | ✔ 6 `vfx_orbital_hover` | ✔ `cpu_orbital_hover` | ✔ `wire_polyhedron` / `crystal_cluster` / `ring_torus_segments` | — |
| 17 | `column` | ✔ `mat_volume_fbm` / `mat_scanline_sweep` | ✔ 1 / 5 | ✔ 同 | ✔ `cylinder_beam` / `crystal_cluster` / `vine_spline` / `spiral_ribbon` | — |
| 18 | `veil` | ✔ **`mat_veil_soft`**（§2-4 低亮雾幕的落点） | ✔ 9 `vfx_drift_curl` | ✔ 同 | ✔ `subdivided_plane` / `tech_panel` | — |
| 19 | `cloth` | ✔ `mat_dissolve_edge`（撕裂/烧蚀）+ `sg_vdisp_*`（低档替代） | — | — | ✔ **`cloth_patch` + Cloth 组件**；ML/MM 顶点位移替代 | — |
| 20 | `fill` | ✔ `mat_ui_fill` / `mat_scanline_sweep` / `mat_grid_cells` | — | ✔（Camera/World Canvas） | — | — |
| 21 | `frame` | ✔ `mat_ui_frame` / `mat_scanline_sweep` | — | ✔（同上） | — | — |

**统计**：21 / 21 层角色全部有承担者。`—` 的分布是设计性的：`light` 只能由局部光族承担（3 个 `—` 是"粒子/网格不产生光"这条纪律的体现，粒子的 `Lights` 模块被明令禁止）；`decal / cloth / fill / frame` 不用 GPU 粒子（前两者是表面语义，后两者在 UGUI 下 GPU 粒子不可用）。

---

## 4. 对 T2c 的输入

### 4.1 样片实现顺序（本报告是唯一允许出现具体特效名的位置，ADR-010 §10-1）

沿用 `T2A_REPORT.md` §5.4 的九格建议：`{shield, chain_link, dissolve_out} × {ice, lightning, poison} × cartoon`，2D / 3D 各一组共 18 个预制体。T2b 从技术族角度确认这个选择是**恰当的**，并给出理由的技术版：

| 原型 | 检验的技术族路径 | T2b 规格中的对应节 |
|---|---|---|
| B04 `shield` | 网格族的壳生成 + 预破碎 + Rigidbody 全链路；材质的菲涅尔与涟漪；局部光的 `breathe`/`steady` | `MESH_LIGHT` §4.4 / §4.17 / §4.17b、`MATERIAL` §8 变体 18/19/20 |
| A10 `chain_link` | 网格族的**非 billboard 拓扑**（折线 + 分叉）+ 材质的双阈值（硬核 + 宽晕）+ 多节点局部光的 `followTarget = latestNode` | `MESH_LIGHT` §4.6、`MATERIAL` §3.3 `sg_threshold_dual` / 变体 4、`MESH_LIGHT` §6.2 |
| C06 `dissolve_out` | 材质的阈值推进（`sg_threshold_grow` 四模式）+ **粒子从阈值边缘发射** + `rendererRef` 为空时的退化 | `MATERIAL` §3.3 / 变体 20、`PARTICLES` §3 模板 13、`COMPILER_BOUNDARY_V2` §5.6 |

| 元素 | 检验的技术族特征 |
|---|---|
| `ice` | `sg_noise_voronoi_cell` + 硬阈值 + `crystal_cluster` + `voronoi_prefracture` + `steady` 光 |
| `lightning` | `sg_noise_jagged_1d` 的 `rephaseRate` 跳变 + `jagged_polyline` 递归分叉 + `strobe` 光 + `vfx_instant_rephase` 模板 |
| `poison` | `sg_noise_bubble_field` + `sg_vdisp_bulge_pulse` + `vfx_gravity_drag_split` 的粘滞参数 + `breathe` 光 |

风格：`cartoon`（T2a 建议，理由不变）。

### 4.2 先做哪些子图 / 模板 / 生成器（实现优先级）

按"九格样片的最小闭包 + `ELEMENT_CATALOG_v1.md` §5 敏感度总表的 H 格"排序。**第一批（P0，九格样片的硬依赖，必须先做）**：

| 类 | id | 服务 |
|---|---|---|
| 主图 | `SG_VfxSurface` · `SG_VfxAdditive` · `SG_VfxLit` | 全部（`SG_VfxCanvas` 留 P2，九格无 F 类） |
| 噪声 | `sg_noise_fbm_aniso` · `sg_noise_voronoi_cell` · `sg_noise_jagged_1d` · `sg_noise_bubble_field` | 三元素的主形态 |
| SDF | `sg_sdf_primitive_2d` · `sg_sdf_primitive_3d` · `sg_sdf_crack_branch` | 壳 / 涟漪 / 裂纹 |
| 阈值 | `sg_threshold_soft` · `sg_threshold_hard` · `sg_threshold_grow` · `sg_threshold_dual` | 四型全需要（`dither` 留 P2） |
| 边缘 | `sg_edge_fresnel` · `sg_edge_sdf_rim` · `sg_edge_outline_sdf` · `sg_edge_dark_rim` | 卡通描边三路 |
| 流动 | `sg_flow_uv_scroll` · `sg_flow_polar` · `sg_flow_along_axis` | |
| 顶点位移 | `sg_vdisp_axial_stretch` · `sg_vdisp_bulge_pulse` · `sg_vdisp_shell_extrude` | 舌形 / 鼓泡 / 描边壳 |
| 合成 | `sg_comp_hdr_grade` · `sg_comp_palette_lut` · **`sg_comp_glow_stack`** · `sg_comp_veil_layer` · `sg_passthrough_*` | **`glow_stack` 是 P0 最高优先级**：REFERENCE_ANALYSIS §2 六条准则里有三条（分级 / 硬柔并存 / 光晕溢出）依赖它，它也是用户点名要"可调教"的部分 |
| 风格 | `sg_style_none` · `sg_style_cartoon` | 像素留第二批 |
| VFX 模板 | `vfx_instant_rephase` · `vfx_gravity_settle` · `vfx_gravity_drag_split` · `vfx_burst_radial` | 三元素 + 消散边缘发射 |
| CPU 变体 | 上述四个的 `cpu_*` 对应 | ML/MM 档必需 |
| 生成器 | `shell_polyhedron` · `voronoi_prefracture` · `jagged_polyline` · `crystal_cluster` · `subdivided_plane` · **`radial_spike_array`** | 三原型的几何 + ADR-010 §4bis-8 新增项（应在首批验证其读感） |
| 组件 | `VfxController` · `VfxParameterBlock` · **`VfxLightBeat`** | 三者缺一不可 |

**P0 合计**：3 主图 + 25 子图 + 4 VFX 模板 + 4 CPU 变体 + 6 生成器 + 3 组件。

**第二批（P1，元素敏感度 H 格的补齐）**：`sg_noise_curl` · `sg_noise_stripe_polar` · `sg_sdf_rune_ring` · `sg_sdf_sacred_pattern` · `sg_sdf_grid` · `sg_sdf_scanline` · `sg_comp_smoothmin` · `sg_comp_dark_core` · `sg_comp_refract` · `sg_style_pixel` · `sg_flow_grid_snap` · `sg_flow_time_quantize` · `sg_vdisp_grid_snap` + 模板 1/5/6/7/8/12 + 生成器 `rock_chunk` / `tendril_blob` / `wire_polyhedron` / `vine_spline` / `cylinder_beam` / `ring_torus_segments` / `sweep_band`。

**第三批（P2）**：`SG_VfxCanvas` + `mat_ui_*` + 伪粒子 + 其余生成器与模板。

### 4.3 实现顺序上的两条硬性建议

1. **`sg_comp_glow_stack` 与 `sg_comp_hdr_grade` 必须在任何形态子图之前完成并跑通谓词 HG-1/HG-2**。理由：ADR-010 §0 记录的失败判定是"简单、粗糙、没有设计感"，REFERENCE_ANALYSIS §2 把它拆成六条可操作准则，其中"亮度分级"与"光晕溢出"是最先被肉眼判定的两条。先做形态再补辉光，会得到一批"形状对了但看起来还是廉价"的产物，返工成本高。
2. **`VfxLightBeat` 必须与第一个材质变体同批完成**。理由：局部光是一等成员（ADR-010 §5），且 `_BeatValue` 是材质与光的耦合通道（`MATERIAL` §5.8 / `MESH_LIGHT` §6.7）。若先做材质后补光，`flickerCoupling` 会成为事后加装的补丁，容易出现"光与晕各闪各的"——那恰是廉价感的来源之一。

### 4.4 建议 T2c 一并产出的两个配置文件

- `docs/design/paradigm/gallery-comparison.json`（对比配置，格式见 `GALLERY_SPEC.md` §6.5）。
- `Assets/VFX/Gallery/GalleryPages.asset` 的初始 7 页内容（格式见 `GALLERY_SPEC.md` §3.3 与 §4.2）。

---

## 5. 对 `T2A_REPORT.md` §6 未决问题 6~10 的建议

问题 1~5 已由 ADR-010 §4bis 裁定，不再讨论。

| # | 问题 | T2b 建议 | 理由（技术族规格视角） |
|---|---|---|---|
| 6 | **A07 地面范围的三段节拍**是否拆成两个原型（预警 / 爆发） | **保持合并**（同意 T2a） | 控制器 v2 的阶段表是任意阶段集合（`COMPILER_BOUNDARY_V2.md` §5.2），`telegraphDuration = 0` 与"没有预警阶段"在产物上完全等价——阶段表里就不会有那一项。拆成两个原型只会多一份层结构声明而没有任何编译期或运行时差异。若将来需要"单独复用预警"，编排级的内联展开（§5.9）已经能表达 |
| 7 | **B07 与 F 类无局部光**的两处例外是否认可 | **认可**，但建议把纪律表述改为"发光职责必须存在，不必然是 Light 组件" | `MESH_LIGHT` §6.6 的 `光:烘` 机制已经把"局部光"从"必须有 Light 组件"松绑为"必须有发光职责的载体"（ML 档全部六档产物都没有 Light 组件，但发光职责由材质承担且有谓词 LT-4 保证）。B07（多实例并存）与 F 类（UGUI 不受 Light2D 影响）在这个框架下不是"例外"，而是"永远走 `MaterialOnly` 分支的层"——它们与 ML 档的其他层用的是同一条代码路径。**这使例外从纪律豁免降级为参数取值**，一致性问题消失 |
| 8 | **`element.intensity` 与标准参数 `intensity` 命名易混**，是否改名 | **改为 `element.strength`**（同意 T2a），且建议在 T2c 定稿 schema 时一并改 | 本卡的四份技术族规格中，`intensity` 一律指标准参数（材质的 `_Intensity`、光的 `intensity`、粒子的 `Intensity`），元素浓度只在元素目录出现。两个名字若都叫 `intensity`，绑定表的 `path` 会出现 `element.intensity` 与 `material._Intensity` 并列，读者无法从名字判断哪个是哪个。改名的成本是 schema 一处 + 元素目录一处 |
| 9 | **T3/T4 铺开顺序：先 2D 还是先 3D** | **3D 先行**（同意 T2a），并补一条技术理由 | T2a 的理由是"几何族与局部光在 3D 表现力更完整，2D 投影是其子集"。技术族规格给出第二条：**2D 的实现是 3D 实现的降级路径而不是平行路径**——`sg_sdf_primitive_3d` 在 2D 编译期替换为 2D 版、`sg_edge_fresnel` 替换为 `sg_edge_sdf_rim`、管替换为带、Cloth 替换为顶点位移波、GPU 粒子在 4 条判定下强制降 CPU。先做 3D 会自然产出这些替换的上游；先做 2D 则要在做 3D 时反向推导，且无法验证替换的正确性（没有被替换者可比） |
| 10 | **多元素**：熔岩 / 蒸汽等固定组合是否 T4 单列 | **不单列**，但建议 T4 引入"元素组合预设"（`ElementCombo`）作为**引用而非新元素** | 按层覆写（`elementOverride`）已经能表达"核心是火、外壳是土"。真正缺的是"每次都要手写同一组覆写"的便利性。`ElementCombo` = 一份具名的 `{roleGlob → elementId}` 映射，编译期展开为逐层 `elementOverride`——它不进元素轴（元素仍是 13 个），只是一个宏。这样既避免元素数膨胀，又避免用户重复劳动。是否需要由 T4 的实际使用频次决定 |

---

## 6. 新增未决问题

| # | 问题 | 影响 | T2b 建议 |
|---|---|---|---|
| 11 | **画廊场景是否进 git**。`Assets/VFX/Gallery/**`（2 个 `.unity` + Volume profile + 页配置 + 格线 sprite）是开发期工具还是交付产物？若进仓，它会成为唯一一处"含相机与后处理的 Unity 资产"，需要在规则文档里明确它不受产物纪律约束 | 仓库内容边界；`GALLERY_SPEC.md` §7.2 的表述是否需要升格为规则 | 建议**进仓**（ADR-010 §8 明确它是"T2 必交产物，也是以后每批内容的标准验收面"），并在 `docs/rules/` 的资产布局文档中加一行"`Assets/VFX/Gallery/**` 是场景工具，不受产物审计约束，不在依赖白名单内" |
| 12 | **`C_over`（透明叠加层）的估计系数需要经验校准**。`COMPILER_BOUNDARY_V2.md` §4.4 的 32×32 粗光栅 + 第 95 百分位是一个未经验证的取法 | 六档预算的一项可能过松或过紧 | 建议 T2c 用九格样片的实测（画廊截帧 + Frame Debugger 的 overdraw 视图）校准一次，然后升 `AssetAllowList.json` 的 schema 版本记录系数 |
| 13 | **`VfxLightBeat` 的 3D 光强单位换算**（`MESH_LIGHT` §6.4 的 `unitScale`）依赖项目的 URP 光照单位设置（Lumen / Candela / 任意）。编译期烘死一个常数意味着"换了光照单位设置的用户拿到的产物光强不对" | 跨项目可移植性 | 建议在编译报告中显式声明该产物假定的光照单位，并在 `VfxLightBeat` 上暴露一个 `unitScale` 字段供用户一键校正。**不做运行时查询**（查询管线设置在 Player 中不可靠且每帧开销无谓） |
| 14 | **F 类的"双实现常驻"成本**（`PARTICLES` §8.6：真粒子与伪粒子子节点同时存在，一个被禁用）会让 F 类产物的 GameObject 数比其他类多约 3 个 | F 类的结构预算 | 已在 `COMPILER_BOUNDARY_V2.md` §4.2 的分项预算中留了余量。若 T2c 实测发现 F 类频繁触顶，备选方案是"编译期按 recipe 的一个 `canvasHint` 提示只产一种实现，运行时检测到不匹配时给 Console 警告而不切换"——但那牺牲了"同一预制体丢进任何 Canvas 都工作"的自包含性，需要用户拍板 |
| 15 | **`sg_comp_glow_stack` 的 `anisotropy` 需要控制器每帧写世界速度**（`MATERIAL` §5.6 的 `velocity` 模式）。这给控制器加了一个"必须知道自身速度"的职责，而速度对"被外部驱动位置"的原型（`setTravelPose`）是可算的，对"跟随父节点"的原型则需要每帧差分 | 控制器复杂度 | 建议控制器统一用"根节点世界位置的帧间差分 / deltaTime"计算速度（对两种驱动方式都成立），并做一阶低通滤波（避免瞬移导致的速度尖峰）。若 T2c 发现开销不可忽略，可改为仅在有 `anisotropyAxisMode = velocity` 的层时才计算 |
| 16 | **像素风 3D 的"体素三件套"谓词（PX-6）可能过严**。它要求网格顶点吸附 + 法线量化 + 粒子位置吸附全部启用，但某些原型（如纯 quad 构成的 UI 类、无 `SG_VfxLit` 层的纯加法特效）天然缺其中一项 | 谓词的适用范围 | 建议把 PX-6 改为**条件谓词**："若产物含网格族层，则必须顶点吸附；若含 `SG_VfxLit` 层，则必须法线量化；若含粒子层，则必须位置吸附"——即对不存在的类别不作要求。T2c 实现谓词时按此修正 |

---

## 7. 纪律自检（对照 ADR-010 §10 逐条）

| 红线 | 结果 |
|---|---|
| **§10-1 例子不进正文** | **通过**。六份规格文档的正文一律用"层角色 / 技术族 / 参数"表述；具体特效名只出现在本报告 §4.1 的样片建议节。已 grep 复核：六份规格中"元素名 + 形体名"式特效命名零出现；元素名仅作为"元素 → 子图/模板/生成器"速查表的**行标签**（那是元素轴的正当引用，不是特效命名）；无任何章节围绕火球 / 火焰组织——参考图的 A/B/C 组特征被拆解为"各向异性 / 硬柔并存 / 亮度分级"等跨全部 58 原型的技术特征，写入 `sg_noise_fbm_aniso.aniso`、`sg_threshold_dual`、`sg_comp_hdr_grade`、`sg_comp_veil_layer`、`sg_sdf_radial_burst` / `radial_spike_array` 等**通用**子图与生成器 |
| **§10-2 覆盖面声明** | **通过**。六份产出文档开头均有"覆盖面声明（ADR-010 §10-2）"表格，逐轴给出覆盖 |
| **§4 技术族只有 5 族** | **通过**。四份技术族规格覆盖且仅覆盖 5 族；`COMPILER_BOUNDARY_V2.md` 的 fail-closed 第 1 路明确"新增技术族必须先修订 ADR-010 §4 与本文档补谓词"（MUST 级变更） |
| **§4 资产外事项不进技术族** | **通过**。识别组件闭集的排除表（`COMPILER_BOUNDARY_V2.md` §2.3）明列 Camera / Volume / Canvas / Directional Light / AudioSource / Animator / 时间缩放相关 / 场景力场与风区；粒子族禁 `Lights` 与 `ExternalForces` 模块（CP-6）；局部光禁 Directional（LT-2）；辉光层明确"只影响特效自身占据的屏幕区域"且全屏 Bloom 归用户。**画廊场景**含相机与 Volume，但 `GALLERY_SPEC.md` §7 给出三种混入方式的机器检查，且 `Assets/VFX/Gallery/` 刻意不入依赖白名单（GA-9） |
| **§5 禁序列帧 / flipbook / sprite sheet** | **通过，且机器可查**。CP-5 断言零个 `ParticleSystem.textureSheetAnimation`；GP-6 断言模板清单 `usesFlipbook` 全库为 false；PX-5 断言像素风产物零纹理引用；SG-5 断言子图库零纹理采样端口（唯一例外是 `sg_comp_palette_lut` 的可选梯度 LUT，属 ADR-010 §5 明文允许的"渐变查找表"）；`STYLE_IMPL_CARTOON_PIXEL.md` §3.5 给出时间量化 ≠ 序列帧的 6 条定义性对照；`COMPILER_BOUNDARY_V2.md` §2.3 一并排除 `Animator`/`AnimationClip`（与序列帧同源：烘死的时间序列） |
| **§5 形态一律程序化 / 几何一律 Unity 基础或脚本生成** | **通过**。53 个子图全部解析计算（SG-5）；20 个生成器全部脚本生成且确定性可复现（MS-2）；MS-6 断言 `MeshFilter.sharedMesh` 只能是产物生成网格或 Unity 内置基础几何（零外部模型导入） |
| **§5 局部光是一等成员** | **通过**。`MESH_LIGHT` §6 给出统一组件规格；LT-4 断言 `光:烘` 档"降级不等于什么都没有"；未决问题 7 建议把两处"例外"归并为该框架下的正常取值 |
| **§4bis-1 折射可选层自动降级** | **落地**。`MATERIAL` §6 双路 + 运行时检测 + 三方案裁定（本地关键字）+ Queue 表 + 自反射论证；`COMPILER_BOUNDARY_V2.md` §9 的编译期处理与报告声明格式；两条路均不报错 |
| **§4bis-2 UGUI Overlay 双方案** | **落地**。`PARTICLES` §8 完整规格：算法、7 种 motionMode、参数面对齐表（同一 Recipe 字段两种实现）、四挡成本、运行时 `VfxCanvasModeProbe` 检测 |
| **§4bis-3 编排内联** | **落地**。`COMPILER_BOUNDARY_V2.md` §5.9 给出内联展开的五项机制；schema 的链接语义字段保留但不实现 |
| **§4bis-4 像素 3D 体素感** | **落地**。`STYLE_IMPL_CARTOON_PIXEL.md` §3.2 的四种栅格空间 + 3D 默认 `object` + 体素三件套（顶点吸附 / 法线量化 / 粒子吸附）+ PX-6 谓词 |
| **§4bis-5 低端档卡通材质暗边** | **落地**。`MATERIAL` §3.4 `sg_edge_dark_rim`；`STYLE_IMPL_CARTOON_PIXEL.md` §2.4 的三路描边表与观感差异的诚实记录；CT-2 断言 ML/MM 无描边壳且暗边参数非零；MS-10 断言 ML/MM 无 `Outline` 子节点 |
| **§4bis-6 辉光边界与参数化** | **落地**。`MATERIAL` §5 逐个给出 8 个参数的实现方式，参数面共 18 项，三处可调；`STYLE_IMPL_CARTOON_PIXEL.md` §5.3 给两风格 + 写实基线的辉光默认值；`GALLERY_SPEC.md` §5 的 Bloom 可开关与 §4.2 的辉光调教页 |
| **§4bis-7 参考图定位** | **遵守**。参考图作为质感标尺使用：§2 六条准则被逐条映射为**服务全部 58 原型**的技术机制（各向异性噪声参数 / 双阈值 / 强度台阶的数值谓词 / `veil` 子图 / spark 克制的谓词 / 辉光层），§3bis 的 8 参数落进 `MATERIAL` §5；`GALLERY_SPEC.md` §6.5 给出截帧并排对比的脚本方案与 4 个辅助指标。**未按任何一张图组织章节，未设计任何特定特效** |
| **§4bis-8 新增 `radial_spike_array`** | **落地**。`MESH_LIGHT` §4.19 完整规格（长度/密度/随机角/锥度可调 + `bimodal` 长度分布 + 与材质版 `sg_sdf_radial_burst` 的分工判据）；已列入 T2c 的 P0 实现清单 |
| **只新增文件、不修改既有文件** | **通过**。仅新增 7 个 `.md` 到 `docs/design/paradigm/` |
| **无 git 写操作** | **通过**。未 add / commit / push |
| **不跑构建 / 测试** | **通过**。仅做文档内计数的 grep 复核 |
| **UTF-8、中文正文、术语英文括注** | **通过** |
| **规格可实现（实现者不需再做设计决策）** | **通过**。每个子图给出端口与参数；每个模板给出主力场与模块组合；每个生成器给出算法要点与逐档参数取值；每处降级给出替换目标；每处方案选择给出裁定与理由（折射切换、白名单形式、翻页交互、页组织、对比图生成方式、`C_over` 估计法）；每处"两种实现"给出参数面对齐表 |

---

## 8. 建议的验收动作（主 agent）

1. 审 §3 的"21 层角色 × 5 族"覆盖对照表：确认 `—` 格全部是设计性的而非遗漏。
2. 审 `COMPILER_BOUNDARY_V2.md` §6.3 的"总键数 ~112 且不随内容增长"论证——这是用户点名的 v1 僵化问题的解法，是本卡最需要复核的技术判断。
3. 审 `COMPILER_BOUNDARY_V2.md` §8 的"三件套写入面不修订"结论与三条理由（与 ADR-007 §2.1/§2.2 的一致性）。
4. 对 §6 的新增未决问题 11~16 拍板（11 与 16 影响 T2c 的产出范围，建议优先）。
5. 对 §5 的未决 6~10 建议拍板（8 的改名影响 schema 定稿）。

---

## 9. 验收裁定（2026-09-05，用户拍板 + 主 agent 初审）

| # | 议题 | 裁定 |
|---|---|---|
| 8 | `element.intensity` 命名 | **改为 `element.strength`**（代码线 schema 定稿已按此） |
| 6/7/9/10 | T2a 遗留建议 | **采纳** T2b 报告 §5 建议（保持合并 / 认可发光职责表述 / 3D 先行 / ElementCombo 宏不单列元素） |
| 11 | 画廊场景是否进 git | **进仓**。`Assets/VFX/Gallery/**` 是场景工具，不受产物审计约束、不在依赖白名单内；规则文档落地属代码线/T2c |
| 12~15 | 经验校准 / unitScale / F 类双实现 / 速度差分 | **记台账不阻塞**；T2c 实测后再裁 |
| 16 | 像素风 PX-6 体素三件套 | **改为条件谓词**（缺类不作要求）；代码线门禁按此实现 |

主 agent 初审要点：①§3 覆盖对照 `—` 格均为设计性；②`COMPILER_BOUNDARY_V2` §6.3 绑定键不随内容增长论证成立；③§8 三件套不修订与 ADR-007 一致。`STYLE_CATALOG_v1.md` 曾出现换行漂移，已还原，未纳入提交。
6. 通过后提交，并在 `OPTIMIZATION_MASTER_PLAN.md` §8 将 T2b 置 DONE、T2c 解锁；`RECIPE_V2_SCHEMA_DRAFT.md` §11 的未决 1（变体命名规范）与 2（绑定 path 语法）可标记为已由本卡解决（§2 与 `COMPILER_BOUNDARY_V2.md` §6.5）。
