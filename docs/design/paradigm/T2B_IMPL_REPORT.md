# T2b 代码实现交付报告（T2B_IMPL_REPORT）

日期：2026-09-06
执行：T2b 代码实现子 agent（worktree `D:\wt\i2s-t2b`，分支 `task/T2b-technique-families`，基线 `9d14e131`）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（含 §4bis 八条）；规格线（ACCEPTED）：`T2B_REPORT.md` 与四份技术族规格 + `COMPILER_BOUNDARY_V2.md` + `GALLERY_SPEC.md`
用户拍板已落实：#8 `element.strength`（`VfxElementPreset.strength` 字段）；#11 Gallery 进仓（§4 单元 5）；#16 PX-6 条件谓词（`ValidatePixelVoxelTrio`）；#12~15 记台账不阻塞（本报告 §7）。

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 项 | 目标 | 达成 |
|---|---|---|
| 5 技术族骨架资产 / 组件 / 门禁 | 5 / 5 | **材质**：20 个变体 shader + 2 个 include（HLSL 路线，理由见 §5-1）；**GPU 粒子**：未产出 .vfx（已知限制 §6-1），参数面由 CPU 集承载；**CPU 粒子**：14 模板模块谱应用器 + 纪律强制；**网格**：20 生成器（含 `radial_spike_array`）；**局部光**：`VfxLightBeat` 完整组件 |
| 卡通 / 像素 StyleStage | 2 / 2 | `VfxStyleStage.hlsl` 三实现（None/Cartoon/Pixel），`multi_compile_local` 三关键字；像素 = shader 内量化（UV 栅格吸附 + 时间量化 + 颜色带 + alpha 二值化 + Bayer 抖动 + 1px 描边），零低分辨率 RT |
| ElementPreset 格式 + 3 样例 | 3 | `VfxElementPreset`（SO）+ fire / ice / lightning（仅验证格式管线） |
| StylePreset + 2 样例 | 2 | `VfxStylePreset`（SO）+ cartoon / pixel |
| 画廊 2D / 3D | 2 | `VFXGallery_2D.unity` / `VFXGallery_3D.unity` 进仓 + 页资产（结构页 = element:none 中性；范式判定页） |
| 运行时控制器 v2 | 1 | `VfxController`（不复用 `GeneratedVfxController` 代码） |
| 编译器边界 v2 | 1 | `VfxTechniqueFamilyBoundary`（识别闭集 / 白名单根 / 六档成本 / ~112 绑定键） |

**纪律**：不以任何单一特效为目标；具体组合名只出现在本报告 §8 的画廊自检样例清单。

---

## 1. 按单元改动清单（提交列表）

| 单元 | 提交 | 内容 |
|---|---|---|
| 2a | `0ea2440a` | WIP shader 库收编 16 文件（逐个核验完整性，无截断）+ 稳定 GUID meta + 新增 `VFX_GlowStack.shader`（§4bis-6 光晕全参数面）+ `VFX_RefractDual.shader`（§4bis-1 折射双路） |
| 2b | `d28c7982` | `VfxCommon.hlsl` 增 `VfxHdrGrade`（强度台阶，HG-1/HG-2 数值达标）+ 4 个 P0 变体 shader：`VFX_RadialSpike`（§4bis-8 材质路）/ `VFX_BubbleField` / `VFX_VeilSoft`（alpha 硬上限 0.2）/ `VFX_RingPolar` |
| 3 | `cfcaf0d8` | `VfxMeshGenerators`（20 生成器）+ `VfxMeshBuilder`（确定性 RNG/Halton/Fibonacci）+ `VfxLightBeat` + `VfxController` v2 + `VfxParameterBlock` + `VfxUrpCapabilityProbe` + EditMode 测试 102 条 + manifest 启用 cloth 内置模块 |
| 4 | `65e376f2` | `VfxCpuParticleTemplates`（14 模板）+ `VfxElementPreset` / `VfxStylePreset` + 5 个样例资产 + EditMode 测试 38 条 |
| 5 | `6c56e146` | `VfxTechniqueFamilyBoundary`（边界 v2）+ 门禁测试 18 条 + `Assets/VFX/Gallery/**`（两场景 + 页资产 + Volume + 控制器 + Editor 场景构建器）+ Editor/Tests asmdef 补 URP 引用 + manifest 启用 screencapture |

产物位置：
- Shader 库：`project/Assets/VFX/TechniqueFamilies/Shaders/`（20 shader + Includes 2 hlsl）
- Preset 样例：`project/Assets/VFX/TechniqueFamilies/Presets/`（5 资产）
- 画廊：`project/Assets/VFX/Gallery/`（2 场景 + 2 页资产 + Volume + 3 脚本 + 2 asmdef）
- 运行时组件：`Packages/com.vfxcomposer.unity/Runtime/TechniqueFamilies/`（6 脚本）
- Editor 侧：`Packages/com.vfxcomposer.unity/Editor/TechniqueFamilies/`（4 脚本）
- 测试：`Packages/com.vfxcomposer.unity/Tests/EditMode/`（3 个新测试文件，158 条新测试）

---

## 2. 与规格线一致性对照（差异表）

### 2.1 变体 id

| 规格 | 实现 | 差异说明 |
|---|---|---|
| `TECHNIQUE_VARIANT_LIBRARY_v1.md` §3 的 16 材质变体 | 20 个 `.shader`（16 变体 + glow_stack + refract_dual + 2b 补的 4 个中 2 个与库表重合） | 变体语义一一对应；命名用 `VFX_<PascalCase>.shader`，Shader name = `VFXComposer/TechniqueFamilies/<Name>`。`MATERIAL` 规格 §8 的 25 变体表中 F 类（`mat_ui_*` 3 个）与 `mat_volume_march` / `mat_shell_ripple` 等 5 个未单独立 shader（P1/P2 批次，见 §6-3） |
| 网格 20 生成器（`MESH_LIGHT` §3 总览） | 20 / 20 全实现 | id 逐字一致（`crystal_cluster` … `cloth_patch`）；`trail_mesh` / `line_mesh` 是运行时组件不在生成器注册表内（与规格一致） |
| GPU 14 模板 | 0（未产出 .vfx） | 已知限制 §6-1 |
| CPU 14 变体 | 14 / 14 | id 逐字一致（`cpu_buoyancy_turbulence` … `cpu_mesh_shatter`） |
| 局部光 `light_beat` | `VfxLightBeat` 1 组件 | 与 schema 的 `localLightVariant` 枚举一致 |

### 2.2 参数面

| 规格 | 实现 | 差异 |
|---|---|---|
| 光晕 18 参数（`MATERIAL` §5.10） | shader 侧 12 属性 + 编译期 6 项（enabled/layerCount/layerRadiusRatio/layerIntensityRatio/glowShape/anisotropyAxisMode 属预制体结构与绑定来源，不是材质属性） | 与规格的"生效位置"列一致：编译期项在 §5.10 表中本就标注"编译期决定" |
| `VfxLightBeat` 20 参数（`MESH_LIGHT` §6.2） | 19 序列化字段 + `unitScale` | `followTarget` 未实现（需原型层表支持，T2c 输入）；`beatChannel` 0~3 + `phaseOffset` + 双驱动 + `MaterialOnly` 退化 + 风格量化全齐 |
| 控制器 v2（`COMPILER_BOUNDARY_V2` §5） | 阶段表 / sustain 安全阀 / 事件二分 / 出事件双形式 / 池化复位 9 项全实现 | 池化复位第 3 项（`VisualEffect.Reinit`）注释登记为 GPU 粒子接线时补（无 VFX Graph 包依赖）；第 5 项 Cloth 用 `GetComponentsInChildren`（编译器接线后可烘数组） |
| 绑定三段键 ~112（§6.3） | 112 键常量表 + `ResolveBindingKey` fail-closed | 键集逐族与规格 §6.2 各表一致；处理器数组（§6.4 的 Handler 机制）留 T3（本卡交付键闭集与解析） |
| ElementPreset | `strength`（非 intensity）+ 5 色线性 HDR + 四族倾向 | 与 schema `$defs/element` 一致 |

### 2.3 谓词

| 规格谓词族 | 实现落点 |
|---|---|
| HG-1 / HG-2（台阶亮度比） | `VfxHdrGrade` 默认倍率链 0.06/0.30/1.0/4.0（相邻 ≥ 2.0）+ `VfxElementPreset.Validate` + 测试 `ElementSamples_HotStopSatisfiesHG2` |
| SG-5（零纹理采样） | 测试 `ShaderLibrary_ZeroTextureSamplingProperties`（HLSL 路线的等价物：shader 属性零 Texture 型） |
| MV-2（恰一个风格关键字） | `VfxStylePreset.ApplyToMaterial` 互斥切换 + 测试 `StylePreset_AppliesExactlyOneStyleStageKeyword` |
| MV-8（折射双关键字变体） | `multi_compile_local _ _REFRACT_SCENECOLOR` + 测试 `RefractionShader_DeclaresBothRoutes` |
| MS-2/MS-3/MS-4/MS-5（网格确定性/通道/预算/包围盒） | 测试 `AllGenerators_AreSeedDeterministic` / `AssertMeshContract` / `AllGenerators_HonourTightVertexBudget` |
| CP-5（禁 flipbook）/ Lights / ExternalForces | `VfxCpuParticleTemplates.Configure` 构建期强制 + 测试逐模板断言 |
| LT-6/LT-7（闪烁非静态/像素禁阴影） | `VfxStylePreset.Validate`（LT-7）；LT-6 属产物门禁留编译器接线 |
| PX-6（条件式，拍板 #16） | `ValidatePixelVoxelTrio`：缺类不查 + 测试跳过/命中双向 |
| GA-2/3/4/5/8/9/10（画廊） | 场景构建器保证 + `GallerySceneGateTests` 5 条 |
| 156 条全量 | **未全量落地**——本卡交付各族核心谓词的构造性测试 158 条；完整 156 谓词编号体系的逐条映射属 T2c 门禁扩展（见 §6-4） |

---

## 3. 自裁决策与理由

1. **材质族走手写 HLSL 而非 Shader Graph 子图**（任务书预授权路线）：`.shadergraph` / `.shadersubgraph` 是 JSON 图结构，手写不可靠且无公开稳定 API；规格 §12.0 自己也承认图结构不可断言而要求 sidecar。HLSL 路线下"子图"以 include 函数库承载（`VfxCommon.hlsl` = 哈希/噪声/极坐标/色板/台阶/节拍；`VfxStyleStage.hlsl` = 风格插槽三实现），谓词直接对 shader 属性与源码断言，可查性更强。
2. **画廊场景用 Editor 脚本确定性生成**（`GallerySceneBuilder`）而非手写 .unity YAML：场景 YAML 跨版本脆弱、审阅困难；构建器可重跑、diff 可读，且 GA-* 谓词直接对生成结果断言。
3. **`voronoi_prefracture` 的凸胞算法**用"盒体被站点对中垂面依次半空间裁剪"，断面 `colors.a=1` 标记；这是规格 §4.17 算法要点的直接实现，凸多面体裁剪确定性稳定。
4. **`VfxParameterBlock` 的绑定键 id 编码**：v1 范围内用整数段（<100 = 材质、100~199 = 光）承载 keyId→处理器分支；完整的 §6.4 Handler 数组机制（静态方法 + BindingContext struct）留给编译器接线（T3），本卡保证"recipe 字符串不进运行时"的键闭集与 fail-closed 解析。
5. **Cloth / ScreenCapture 内置模块启用**：Unity 自带模块（`com.unity.modules.*`），非外部依赖，零红线冲突。

---

## 4. 测试数字

| 面 | 基线 | 交付 |
|---|---|---|
| .NET（`dotnet test VFXComposer.sln -c Release`） | 1106 通过 / 0 失败 | **1106 通过 / 0 失败 / 12 套件**（基线保持） |
| Unity EditMode 全量 | 688（634 过 / 54 跳过） | **846 总 / 792 过 / 0 失败 / 54 跳过**（基线 688 + 本卡新增 158，只增不破） |
| 本卡新增 EditMode | — | 158 条（单元 3：102；单元 4：38；单元 5：18），全绿 |

前置：`JobExecutorLockHost` 已先行 `dotnet build -c Release` 成功（0 警告 0 错误）。

---

## 5. 画廊自检样例清单（唯一允许出现具体组合名之处）

页资产初始内容（`GalleryPages_3D.asset` / `GalleryPages_2D.asset`）：

| 页 | 行轴 × 列轴 | 固定值 | 格内容 |
|---|---|---|---|
| 1 结构页 | 原型 × 元素 | element=none, style=none, tier=PM | 待 T2c 产物填充（缺格显示"未编译"占位，不报错） |
| 2 范式判定页 | 原型 × 元素（fire / ice / lightning） | style=cartoon, tier=PM | ADR-010 §8 判定面；随 T2c 九格样片（{shield, chain_link, dissolve_out} × {ice, lightning, poison}，沿用 T2B_REPORT §4.1）填充 |

本卡的 shader 库可在画廊中直接手动挂材质自检：`VFX_NoiseFbm2Layer`（fire 色板）、`VFX_NoiseVoronoiCrystal`（ice 色板）、`VFX_NoiseJagged1D`（lightning 色板）+ 各配 `VFX_GlowStack` 子片。

---

## 6. 已知限制与 T2c 输入

1. **GPU 粒子 14 个 .vfx 模板未产出**。原因：项目 manifest 无 `com.unity.visualeffectgraph` 包（引入属依赖变更需人工裁定），且手写 .vfx YAML 不可靠。已按任务书备用路线登记：CPU 14 模板承载全部参数面语义，T2c 引包后按 `VfxCpuParticleTemplates` 的模板对照表逐一作 VFX Graph 版（`PARTICLES` §4.2 的映射表反向可用）。`vfx_strip_trail` / `vfx_mesh_shatter` 本就是跨族降级（TrailRenderer / 预破碎）。
2. **材质 25 变体中 8 个未立独立 shader**：`mat_volume_march`（需 `sg_edge_thickness`，PL 档）、`mat_shell_ripple`、`mat_liquid_blob` 的 smoothmin 已有单独 shader（`VFX_SmoothminUnion`）但未组合折射槽、`mat_ui_fill` / `mat_ui_frame` / `mat_ui_pseudo_particles`（F 类 P2 批次，`SG_VfxCanvas` 主图需 UGUI Graphic 配套）、`mat_grid_cells` 3D 三平面路、`mat_leaf_petal`。P0 清单（T2B_REPORT §4.2）内的变体全部有承载。
3. **`AssetAllowList.json` 未落盘**：白名单清单是人工维护资产（规格 §3.3 明确"只能由人工维护"），本卡交付其代码侧（`AllowedDependencyRoots` 常量 + `IsDependencyAllowed`）；清单文件与 WL-1/WL-2 双向真实性测试属 T2c 编译器接线。
4. **156 谓词编号体系未逐条映射**：本卡 158 条测试覆盖各族核心谓词（对照 §2.3 表）；完整编号→测试的登记表与显式豁免机制（fail-closed 三路的豁免表）属 T2c。
5. **`VfxLightBeat.followTarget` 未实现**（需原型层表的 `latestNode` 语义）；`sg_comp_soft_depth_fade` 的三级降级几何路（domeBillboard）未实现（Glow 现为 quad billboard）。
6. **台账（拍板 #12~15）**：`C_over` 系数待九格样片实测校准；`unitScale` 已作为 `VfxLightBeat` 字段暴露（编译报告声明待编译器）；F 类双实现常驻成本未验证；控制器速度差分（光晕 `velocity` 轴模式的 `_GlowAxisWS` 每帧写入）留编译器接线——shader 端口已备。

---

## 7. 红线自检

| 红线 | 结果 |
|---|---|
| 禁序列帧 / flipbook / sprite sheet | **通过**。shader 库零 Texture 属性（测试断言）；CPU 模板 `textureSheetAnimation.enabled=false` 构建期强制 + 测试；时间量化（`VfxStyleTime` / `beatFrameRate`）是采样时刻量化，形态仍逐像素解析 |
| 资产外不碰 | **通过**。产物侧组件全在识别闭集；画廊场景含 Camera/Volume/平行光但它是场景工具（拍板 #11），且检视光默认 disabled（GA-2）、`Assets/VFX/Gallery/` 不在依赖根（GA-9 测试断言） |
| 不以单一特效为目标 | **通过**。全部资产按形态能力命名（`radial_spike_array` / `bubble_field` / `veil_soft`…），元素名只出现在 preset 样例与本报告 §5 |
| 零新增外部依赖 | **通过**。新启用的 `com.unity.modules.cloth` / `com.unity.modules.screencapture` 是 Unity 内置模块；未引入任何 registry 包 |
| 禁写主仓 / 未 push | **通过**。全部提交在 worktree 分支 `task/T2b-technique-families`，未 push、未碰 master |
| 旧 kind 闭集与旧豁免表不动 | **通过**。未触碰既有 `VfxOutputAuditor` / 旧注册表 / 旧豁免表（T2c 范围） |

---

## 8. 交付定义核对

- [x] 单元 2a / 2b / 3 / 4 / 5 各 ≥1 提交（共 6 个提交含收尾，见 §1）
- [x] EditMode 全量 0 失败（846 总 / 792 过 / 54 跳过；首轮曾有 1 失败 = 既有 A7 编辑器依赖审计命中 `GalleryController.cs`，改整文件 `#if UNITY_EDITOR` 包裹后复绿——画廊本就不进 Player 构建）
- [x] .NET 基线保持（1106 / 0 失败）
- [x] 报告落盘（本文件）
- [x] 未 push；未碰主仓
