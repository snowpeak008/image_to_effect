# 编译器边界修订规格 v2（Compiler Boundary v2）

状态：`DRAFT`（T2b 产出，2026-09-05，待主 agent 验收）
上位规范：`docs/rules/ADR-010_CONTENT_PARADIGM.md`（`CONTENT_PARADIGM_V1`）、`docs/rules/ADR-007_CONTROLLED_PROJECT_MUTATION.md`（`CONTROLLED_PROJECT_MUTATION_V1`，写入面）、`docs/rules/ADR-009_TEMPLATE_VISUAL_QUALITY.md`（谓词方法论）
配套文档：`RECIPE_V2_SCHEMA_DRAFT.md`（本文档修订其编译器相关部分）、`PROTOTYPE_CATALOG_v1.md` §2.4、四份技术族规格、`GALLERY_SPEC.md`

现有代码（**本文档不修改任何代码**，只规定 v2 的边界）：`Editor/Build/VfxCompiler.cs`、`Editor/Build/VfxBindingHandlerRegistry.cs`、`Editor/Domain/VfxBindingKeys.cs`、`Editor/Rules/VfxOutputAuditor.cs`、`ProjectSettings/VFXComposer/VfxProjectRules.json`、`Runtime/Components/GeneratedVfxController.cs`。

---

## 0. 覆盖面声明（ADR-010 §10-2）

| 轴 | 本文档覆盖 |
|---|---|
| **技术族轴** | 5 族**全部**在编译器侧有：识别组件闭集成员、资产准入白名单条目、成本模型成本项、绑定表分表、门禁谓词（每族 ≥ 3 条） |
| **原型轴** | 58 原型 + `orchestration` 的编译语义：任意阶段集合、`sustain` 循环、原型特有事件分派、编排内联展开（ADR-010 §4bis-3） |
| **元素轴 / 风格轴** | 参数合并链的编译期实现与写入面纪律；风格越权检测（`STYLE_IMPL_CARTOON_PIXEL.md` §4 边界表） |
| **维度轴** | 2D / 3D 的组件差异（`Light` vs `Light2D`、`SpriteRenderer` vs `MeshRenderer`、`Rigidbody` vs `Rigidbody2D`）在识别闭集与谓词中分别立法 |
| **档位轴** | 六档全部有可机器计算的成本函数、上限表、超限错误码与降级登记 |

---

## 1. v1 边界的事实清单（修订的起点）

逐条核实自当前 master 代码，不转述：

| # | v1 事实 | 出处 | v2 处置 |
|---|---|---|---|
| 1 | 识别组件仅 `ParticleSystem` / `TrailRenderer`（绑定处理器的 `Particle()` / `Trail()` 两个取组件助手） | `VfxBindingHandlerRegistry.cs` L94-106 | **扩展**为 §2 的 16 类闭集 |
| 2 | 绑定 allow-list = `VfxBindingKeys` 的 22 个显式常量 → `VfxBindingHandlerRegistry` 的显式 lambda；`Apply` 对未登记键抛异常；"never reflected into code" | `VfxBindingKeys.cs`、`VfxBindingHandlerRegistry.cs` L28 | **保留禁反射内核**，改为 §6 的分族三段键 + 处理器工厂，消除"每加一个参数改注册表" |
| 3 | 模板实例化 = `PrefabUtility.InstantiatePrefab` + `UnpackPrefabInstance(Completely)` | `VfxCompiler.cs` L267-269 | v2 无 prefab 模板；改为**从零构建 GameObject + 挂组件 + 赋资产**（§5.1） |
| 4 | 材质克隆：每个 Renderer 的 `sharedMaterial` 深拷贝到临时目录再提交 | `VfxCompiler.cs` L337-349, L413-434 | **保留**该机制与其回滚序列 |
| 5 | 阶段结构固定三阶段 `Launch/Travel/Impact`，控制器字段 `launchRoot/travelRoot/impactRoot` | `VfxCompiler.cs` L294-311、`GeneratedVfxController.cs` | **推翻**：v2 任意阶段集合（§5） |
| 6 | 输出路径守卫 `IsGeneratedPath`（前缀 + 拒 `..`）→ `E600`；依赖闭包限制在 `Generated/` 与 `Templates/` → `E601` | `VfxCompiler.cs` L89-94, L379-384 | **保留**；`Templates/` 换成 `Shared/`（§3.4） |
| 7 | 成本模型 = 模板 manifest 声明的 `estimatedPeakParticles / materials / trails` 简单求和 | `VfxCompiler.cs` L478-494 | **推翻**：改为 §4 的八项分族成本函数 |
| 8 | 结构预算 = `VfxProjectRules.json` 的 `simple/complex` 两档 × `maxGameObjects/maxDepth/maxLocalMaterials/maxLocalTextures`，按 21 个 v1 archetype 映射 | `VfxProjectRules.json`、`VfxOutputAuditor.cs` L54-58, L94-96 | **扩展**：58+1 原型 × 六档 × 分项（§4.2） |
| 9 | 审计器检查：唯一 `IVfxRuntimeEntry`、名称唯一、无 Editor 组件、无预览组件、无缺失脚本、材质/shader 非空、依赖在 `allowedDependencyRoots`、产物内零 Shader 资产（R8016） | `VfxOutputAuditor.cs` | **保留全部**并新增分族谓词（§7） |
| 10 | 原子提交与回滚：随机临时目录 → 校验 → 备份 → 提交 → 失败按备份恢复；manifest `.pending` + `File.Replace` | `VfxCompiler.cs` L387-476 | **保留**（ADR-007 §2.4 已规范化为 MUST） |
| 11 | 三件套写入面（ADR-007 §2.1）：`Assets/VFX/Generated/**`、`ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json`、`Assets/VFX/Recipes/<id>.json` | ADR-007 §2.1 | **结论见 §8**（不修订，理由充分） |

---

## 2. 识别组件闭集扩展

### 2.1 什么是"识别组件闭集"

编译器只会向**这些组件类型**写入，产物中也只允许出现这些组件类型（加上 Unity 必需的 `Transform`）。审计器对闭集外的任何组件一律拒绝。这是"预制体完全自包含、对外部零假设"的机器保证。

### 2.2 闭集（16 类 + 2 类运行时脚本）

| # | 组件 | 族 | 写入面纪律（编译器允许写哪些字段） | 维度 |
|---|---|---|---|---|
| 1 | `MeshFilter` | mesh / material | `sharedMesh`（**只能指向产物目录的生成网格或 Unity 内置基础几何**） | 2d,3d |
| 2 | `MeshRenderer` | mesh / material | `sharedMaterial`（单材质，禁多材质槽）、`shadowCastingMode`（默认 Off）、`receiveShadows`（默认 false）、`lightProbeUsage`（默认 Off）、`reflectionProbeUsage`（默认 Off）、`sortingLayerID` / `sortingOrder`（2D）。**禁写** `probeAnchor`、`staticShadowCaster` 等依赖场景的字段 | 2d,3d |
| 3 | `SpriteRenderer` | material | `sharedMaterial`、`sprite`（**只能是产物目录内生成的 1×1 白 sprite 或 null**——形态来自材质，不来自 sprite 资产）、`drawMode = Simple`、`sortingLayerID` / `sortingOrder`、`color`。**禁写** `spriteSortPoint = Pivot` 之外的值 | 2d |
| 4 | `ParticleSystem` | cpu_particles | 见 `TECH_FAMILY_SPEC_PARTICLES.md` §4.3 的模块允许/禁止表 | 2d,3d |
| 5 | `ParticleSystemRenderer` | cpu_particles | `renderMode`、`sharedMaterial`、`mesh`、`sortingLayerID` / `sortingOrder`、`sortMode`、`alignment`、`velocityScale`、`lengthScale`。**禁写** `trailMaterial` 为白名单外材质 | 2d,3d |
| 6 | `VisualEffect` | gpu_particles | `visualEffectAsset`（白名单）、`startSeed`、`resetSeedOnPlay=false`、`initialEventName`（空 = 不自动播）、全部 Exposed Property 的 `Set*` | 2d,3d |
| 7 | `Light` | local_light | `type`（**仅 Point / Spot**）、`color`、`intensity`、`range`、`innerSpotAngle` / `spotAngle`、`shadows`、`shadowStrength`、`renderMode`、`cullingMask`（**必须保持 Everything**——改它是对用户场景 layer 的假设）。**禁 Directional**（全局光，ADR-010 §4 资产外） | 3d |
| 8 | `Light2D`（URP 2D） | local_light | `lightType`（**仅 Point / Freeform / Sprite**，禁 `Global`）、`color`、`intensity`、`pointLightInnerRadius` / `pointLightOuterRadius`、`falloffIntensity`、`shadowsEnabled`、`blendStyleIndex`（默认 0）、`targetSortingLayers`（**必须保持全部**） | 2d |
| 9 | `Rigidbody` | mesh（预破碎） | `isKinematic`、`useGravity`、`mass`、`drag`、`angularDrag`、`interpolation`、`collisionDetectionMode`、`excludeLayers`。**禁写** `constraints` 引用外部 | 3d |
| 10 | `Rigidbody2D` | mesh（预破碎） | 同上的 2D 对应字段 | 2d |
| 11 | `BoxCollider` / `MeshCollider` / `SphereCollider` / `CapsuleCollider` | mesh（预破碎） | `sharedMesh`（`MeshCollider` 必须 `convex=true`）、尺寸、`isTrigger=false`、`excludeLayers`、`sharedMaterial`（**必须 null**——PhysicMaterial 是资产，特效不带） | 3d |
| 12 | `PolygonCollider2D` / `BoxCollider2D` | mesh（预破碎） | `points` / `size`、`isTrigger=false`、`excludeLayers` | 2d |
| 13 | `Cloth` | mesh | `coefficients`、`bendingStiffness`、`stretchingStiffness`、`damping`、`externalAcceleration`、`randomAcceleration`、`worldVelocityScale`、`friction`、`useTethers`、`useGravity`、`solverFrequency`、`selfCollisionDistance`。**`capsuleColliders` / `sphereColliders` 必须为空** | 3d |
| 14 | `TrailRenderer` | mesh | `time`、`minVertexDistance`、`widthCurve` / `widthMultiplier`、`colorGradient`、`alignment`、`textureMode`、`numCornerVertices` / `numCapVertices`、`sharedMaterial`、`autodestruct=false`、`emitting`、`sortingLayerID` / `sortingOrder` | 2d,3d |
| 15 | `LineRenderer` | mesh | 同上 + `positionCount`、`useWorldSpace`、`loop` | 2d,3d |
| 16 | `SortingGroup` | 通用（2D） | `sortingLayerID`、`sortingOrder` | 2d |
| 17 | `CanvasRenderer` + UGUI `Graphic` 派生（产品自有 `VfxUiGraphic`） | material（F 类） | `material`、`raycastTarget=false`（**必须**：特效不得吃点击）、`color` | 2d(UGUI) |
| 18 | `DecalProjector`（URP） | material | `material`、`size`、`pivot`、`fadeFactor`、`renderingLayerMask`（保持默认）。**可选组件**：当 URP Renderer 未启用 Decal Renderer Feature 时不可用，因此编译器为 `decal` 角色默认用贴地 quad，`DecalProjector` 需 recipe 显式选择并接受"用户未加 Feature 则不显示"的后果（编译报告登记） | 3d |

**运行时脚本（产品自有，2 类 + 4 个探针/辅助）**：

| 组件 | 位置 | 职责 |
|---|---|---|
| `VfxController`（v2） | 预制体根，唯一 | §5 |
| `VfxParameterBlock` | 预制体根 | 序列化参数值 + Inspector 面（§5.7） |
| `VfxLightBeat` | `light` 层节点 | `TECH_FAMILY_SPEC_MESH_LIGHT.md` §6 |
| `VfxUrpCapabilityProbe` | 预制体根（仅当有折射层） | `TECH_FAMILY_SPEC_MATERIAL.md` §6.2 |
| `VfxCanvasModeProbe` | 预制体根（仅 F 类） | `TECH_FAMILY_SPEC_PARTICLES.md` §8.6 |
| `VfxDebrisDriver` | 预破碎层节点（仅 ML 档无刚体路径） | `TECH_FAMILY_SPEC_MESH_LIGHT.md` §4.17b |

### 2.3 明确排除（产物中出现即 FAIL）

`Camera`、`Volume` / `VolumeProfile`、`Canvas`（特效挂在用户的 Canvas 下，不自带）、`CanvasScaler`、`GraphicRaycaster`、`EventSystem`、`AudioSource`、`Animator` / `Animation`、`PlayableDirector`、`WindZone`、`ParticleSystemForceField`、`ReflectionProbe`、`LightProbeGroup`、`Terrain*`、`NavMesh*`、`Joint` 系列、任何 `UnityEditor` 程序集组件、`VfxProjectRules.json` 的 `forbiddenRuntimeComponentTypeNames` 全部成员。

`Animator` 的排除值得单说：形态一律程序化（ADR-010 §5），用 Animator 驱动参数等于把节拍从控制器搬到一个不可机器检查的动画资产里，且 AnimationClip 是"烘死的时间序列"——与序列帧禁令同源。节拍一律由 `VfxController` 的阶段表 + 材质的 `_Progress` 驱动。

---

## 3. 资产准入白名单

### 3.1 问题

用户（或 AI）不得引入任意 shader / VFX 资产 / 网格。v1 靠"依赖闭包必须在 `Generated/` 与 `Templates/` 内"（`E601`）+ "产物内零 Shader 资产"（R8016）达成。v2 的资产种类更多（shader 主图、shader 子图、VFX 模板、生成网格、StylePreset、ElementPreset、变体清单），需要更精确的准入。

### 3.2 方案裁定：**GUID 白名单 + 路径根双层**

| 方案 | 描述 | 裁定 |
|---|---|---|
| A：纯路径白名单 | 依赖必须在 `Assets/VFX/Shared/**` 下 | **不足**。路径可被移动/新增：任何人往 `Shared/Shaders/` 里放一个新 shader 就自动获得准入，白名单形同虚设 |
| B：纯 GUID 白名单 | 一份清单枚举全部允许资产的 GUID | **不足**。GUID 清单需要与资产同步维护；单靠它无法阻止"产物依赖了一个白名单内 shader 但该 shader 依赖了别的东西"（传递依赖） |
| **C：GUID 白名单（直接依赖）+ 路径根（传递闭包）—— 采用** | ① **直接引用**（材质的 shader、`VisualEffect.visualEffectAsset`、`MeshFilter.sharedMesh`、`StyleStage` 子图）必须 GUID 命中白名单清单；② **整个依赖闭包**（`AssetDatabase.GetDependencies(path, true)`）必须落在允许的路径根内 | **是**。①防"合法路径下的非法新资产"，②防"白名单资产的传递依赖越界"。两层缺一不可 |

### 3.3 白名单清单的形状与真实性保证

```
ProjectSettings/VFXComposer/AssetAllowList.json
{
  "schemaVersion": 1,
  "shaderGraphs":  [ { "id": "SG_VfxSurface",  "guid": "...", "path": "Assets/VFX/Shared/Shaders/SG_VfxSurface.shadergraph",  "sha256": "..." }, … ],
  "subgraphs":     [ { "id": "sg_noise_fbm_aniso", "guid": "...", "path": "…", "sha256": "…" }, … ],
  "vfxTemplates":  [ { "id": "vfx_buoyancy_turbulence", "guid": "…", "path": "…", "sha256": "…" }, … ],
  "meshGenerators":[ { "id": "crystal_cluster", "version": "1.0.0", "codeSha256": "…" }, … ],
  "presets":       [ { "kind": "style|element", "id": "cartoon", "guid": "…", "sha256": "…" }, … ],
  "allowedDependencyRoots": [ "Assets/VFX/Shared/", "Assets/VFX/Generated/",
                              "Packages/com.vfxcomposer.unity/Runtime/",
                              "Packages/com.unity.render-pipelines.core/",
                              "Packages/com.unity.render-pipelines.universal/",
                              "Packages/com.unity.visualeffectgraph/",
                              "Packages/com.unity.ugui/" ]
}
```

**为什么放在 `ProjectSettings/VFXComposer/`**：与 ADR-007 §2.1 的审计元数据单点同一命名空间；它**不是** AI 构建可写的路径（ADR-007 §2.1 明确禁止写 `ProjectSettings/**` 其余路径）——白名单只能由人工维护，这正是它作为准入闸的前提。

**双向真实性（谓词 WL-1/WL-2）**：

- 清单里的每一项必须解析到存在的资产且 `sha256` 匹配（防"清单登记了一个已被改内容的资产"）。
- `Assets/VFX/Shared/{Shaders,Subgraphs,VfxTemplates}/` 下的每一个资产必须在清单内（防"合法路径下的未登记新资产"）。这条使 §3.2 方案 A 的漏洞被堵死。

**网格生成器不是资产而是代码**，因此登记的是 `codeSha256`（生成器静态类源文件的哈希）与 `version`；生成器改了但 version 没改 → 谓词 WL-3 FAIL（防止"同一 version 产出不同网格"破坏确定性）。

### 3.4 依赖根的修订

v1 的 `allowedDependencyRoots` 含 `Assets/VFX/Effects/` 与 `Assets/VFX/Templates/`。v2：

- `Assets/VFX/Templates/` **退役**（ADR-010 §9 清理旧模板库）。
- `Assets/VFX/Effects/` **退役**（旧内容层）。
- 新增 `Packages/com.unity.visualeffectgraph/`（GPU 粒子族的运行时依赖）。
- `Assets/VFX/Shared/` 保留并成为唯一的共享资产根。

**ADR-007 §2.2 的 Shared 只读政策在 v2 保持不变**：AI 构建期 `Assets/VFX/Shared/**` 一律只读，缺失即失败、不自动补建。子图库 / 模板库 / 白名单清单全部是**人工维护的预置资产**，这与 ADR-007 §2.2 对 `Shared/Shaders` 的定性完全一致。

### 3.5 三层准入检查的时序

```
DryRun 阶段（零写入）：
  1. recipe 解析 + schema 校验                          → E1xx
  2. 目录引用校验（archetype/role/variant/element/style） → E2xx
  3. 变体清单查表 → 得到该层需要的直接资产 GUID 集合
  4. 每个 GUID 查 AssetAllowList                        → E401 未准入
  5. 成本模型计算 + 六档上限                            → E300
Build 阶段（临时目录）：
  6. 构建产物
  7. AssetDatabase.GetDependencies(prefab, true) 闭包检查 → E601 越界
  8. 门禁谓词全跑                                        → E5xx
  9. 通过 → 提交（备份-替换-可回滚）
```

第 4 步在 DryRun 就拦截，保证"未准入资产从不进入构建流程"（fail-closed 的零写入语义）。

---

## 4. 成本模型：六档可机器计算

### 4.1 八项成本

以 `PROTOTYPE_CATALOG_v1.md` §2.4 的基准预算表为起点，给出每项的**机器可计算函数**。全部为编译期静态计算，不需要 PlayMode。

| 项 | 符号 | 计算函数 | 数据来源 |
|---|---|---|---|
| GPU 粒子 | `C_gpu` | `Σ_{gpu 层} VisualEffect.Capacity` | 层参数（编译期烘死的 `Capacity`） |
| CPU 粒子峰值 | `C_cpu` | `Σ_{cpu 层, 含 SubEmitter} max(maxParticles, ceil(rateOverTime × lifetimeMax) + Σ bursts.count)` | ParticleSystem 模块字段 |
| 材质噪声采样层数 | `C_mat(层)` | 该层启用的全部子图的 `sampleCost` 之和（子图清单声明）。**逐层判定，不求和** | 子图清单 + 变体清单 |
| 局部光数 | `C_light` | `Light` + `Light2D` 组件数；`C_shadow` = 其中 `shadows != None` 的数 | 组件计数 |
| 破碎块 | `C_frag` | `Σ_{预破碎层} fragmentCount` | 网格 sidecar |
| Cloth 档 | `C_cloth` | `max_{cloth 层} clothTier`（`0=禁用 / 1=低 / 2=中 / 3=高`），且 `Σ cloth 顶点数` 单列 | Cloth 组件 + 网格顶点数 |
| 透明叠加层（overdraw） | `C_over` | 见 §4.4 | 层的包围盒重叠分析 |
| 程序化网格顶点 | `C_vert(层)` | 该层全部 Mesh 的 `vertexCount` 之和。**逐层判定** | Mesh 资产 |

### 4.2 六档上限表

| 项 | ML | MM | MH | PL | PM | PH | 判定方式 |
|---|---|---|---|---|---|---|---|
| `C_gpu` | 0 | 0 | 2 000 | 5 000 | 20 000 | 100 000 | 全预制体求和 |
| `C_cpu` | 60 | 150 | 300 | 400 | 600 | 1 000 | 全预制体求和 |
| `C_mat` | 1 | 2 | 3 | 2 | 3 | 4 | **逐层**（MH 的 "2+细节" 记为 3；PH 的 "3+顶点位移" 记为 4） |
| `C_light` | 0 | 1 | 2 | 2 | 3 | 4 | 全预制体 |
| `C_shadow` | 0 | 0 | 0 | 0 | 1 | 2 | 全预制体 |
| `C_frag` | 8 | 16 | 32 | 48 | 96 | 200 | 全预制体求和 |
| `C_cloth` | 0 | 0 | 1 | 1 | 2 | 3 | 全预制体取最大 |
| `C_over` | 2 | 3 | 4 | 4 | 6 | 8 | 见 §4.4 |
| `C_vert` | 256 | 512 | 2 048 | 2 048 | 8 192 | 32 768 | **逐层** |

新增的**结构预算**（替代 `VfxProjectRules.json` 的 `simple/complex` 两档）：

| 项 | ML | MM | MH | PL | PM | PH | 备注 |
|---|---|---|---|---|---|---|---|
| GameObject 数（不含碎块与辉光子节点） | 12 | 16 | 24 | 24 | 32 | 40 | 层数上限约 20；58 原型中层数最多的原型为 8 层 |
| 碎块子节点（单列） | `C_frag` | 同 | 同 | 同 | 同 | 同 | 与 `C_frag` 同额度，不占主预算 |
| 辉光子节点（单列） | `Σ glow.layerCount` | 同 | 同 | 同 | 同 | 同 | 同上 |
| 层次深度 | 4 | 4 | 5 | 5 | 5 | 5 | 根 / Layers / layer / Glow_k 或 Frag_k / （描边壳）|
| 产物内 Material 数 | 层数 + 辉光片数 + 描边壳数 | 同 | 同 | 同 | 同 | 同 | 每个渲染器一张克隆材质 |
| 产物内 Texture 数 | 0 | 0 | 0 | 0 | ≤ 2 | ≤ 2 | **零纹理是默认**（形态全程序化）；PM/PH 允许 ≤ 2 张色板 LUT（ADR-010 §5 允许的辅助素材） |
| 产物内 Shader 数 | 0 | 0 | 0 | 0 | 0 | 0 | 沿用 R8016 |

**为什么把碎块与辉光子节点从 GameObject 主预算里分出来**：v1 的 `maxGameObjects: 10/16` 是为"一个 prefab 模板 ≈ 1~3 个 GameObject"的旧范式定的。v2 的一个 200 块预破碎层就有 200 个子节点——把它们混进同一个数字会让预算表要么无意义（放宽到 250）要么无法编译（卡在 40）。分项预算使每一类结构有各自的、有语义的上限。

### 4.3 辉光子节点计入 overdraw

`Σ_{层} glow.layerCount` 直接计入 `C_over`（`TECH_FAMILY_SPEC_MATERIAL.md` §10.3）。这是"多层辉光的成本诚实可数"的实现（该文档 §5.1 的设计理由之一）。

### 4.4 `C_over` 的计算方法

真实的 overdraw 需要渲染。编译期用**保守的静态估计**：

```
1. 取每个透明层（含 Glow_k、粒子层）在预制体局部空间的包围盒，投影到三个主平面（3D）或 XY 平面（2D）
2. 在投影平面上做 32×32 的粗网格光栅化，每格记录覆盖它的层数
3. C_over = 该网格的第 95 百分位覆盖数（不用最大值：单个格的极值常来自包围盒角落的空隙，不代表实际 overdraw）
4. 粒子层的包围盒按 "发射体形状 + 最大速度 × 最大寿命" 膨胀
```

**这是估计不是测量**，因此：

- 判定用**警告 + 硬上限双阈值**：`C_over > 上限` → 警告 `W403`（不阻塞，因为估计偏保守）；`C_over > 上限 × 1.5` → 错误 `E300`（明显超出，估计误差不足以解释）。
- 编译报告输出网格热力图的文本摘要（各百分位数），供人工判读。
- 画廊的截帧验收（`GALLERY_SPEC.md` §6）是这条估计的经验校准面：若实测帧率与估计系统性不符，调整膨胀系数并升白名单 schema 版本。

诚实边界：本模型**不预测帧率**，只预测"透明层堆叠的层数"这一个可静态计算的量。

### 4.5 超限处理与错误码

| 码 | 条件 | 行为 |
|---|---|---|
| `E300` | 任一项超硬上限 | 编译失败。报告必须列出：超限项、实际值、上限、**每个贡献层的分摊值**（降序）、以及"禁用哪些可选层可通过"的建议集合（贪心：按分摊值降序禁用可选层直到达标） |
| `E301` | 降级表找不到替代族（目录缺项） | 编译失败（目录错误，不是 recipe 错误） |
| `E302` | 分项结构预算超限（GameObject / 深度 / Material / Texture / Shader） | 编译失败 |
| `W403` | `C_over` 在 [上限, 上限×1.5) | 警告，登记 |
| `W404` | 自动降级已应用（族替换 / 参数截断 / 层关闭） | 警告，每次降级一条，含降级前后值 |

### 4.6 降级的执行顺序（确定性要求）

同一 recipe + 同一档位必须产出字节相同的预制体，因此降级顺序必须固定：

```
1. 应用原型目录的档位降级表（层 enabled / 族替换 / 变体替换）
2. 应用 recipe 的 tierOverrides（覆盖第 1 步）
3. 应用风格约束与档位上限的"取小"
4. 应用参数上限截断（逐层，按 layerId 的 Ordinal 序）
5. 若仍超限：按"可选层的 priority 降序 → layerId Ordinal 序"贪心禁用，直到达标或可选层用尽
6. 若禁用全部可选层后仍超限 → E300
```

第 5 步的排序必须是全序（priority 相同时用 layerId 的 Ordinal 比较），否则确定性不成立。

---

## 5. 运行时控制器 v2 规格

### 5.0 与 v1 的关系

`GeneratedVfxController` 的三阶段结构（`launchRoot/travelRoot/impactRoot` + `PlayStage` 的 stop-all-then-play 语义 + `ClearTrails` + `ResetForPool`）是雏形参考，**v2 不复用**（ADR-010 §9 / `T2A_REPORT.md` §5.2）。可继承的具体做法有三条：池化复位时清 Trail、阶段切换前 `StopEmittingAndClear`、`IsAlive` 的"全部清空才算完"判定。

### 5.1 结构

```
<recipeId>__<tier>                    GameObject（预制体根）
├─ VfxController                      唯一 IVfxRuntimeEntry
├─ VfxParameterBlock                  参数值 + Inspector
├─ VfxUrpCapabilityProbe              可选（有折射层时）
├─ VfxCanvasModeProbe                 可选（F 类）
└─ Layers/                            纯容器节点（无组件）
   ├─ <layerId_0>/ …
   └─ <layerId_n>/ …
```

**没有阶段容器节点**。v1 用 `Launch/Travel/Impact` 三个 GameObject 分组，v2 的层可以跨阶段存在（`sustain` 层贯穿全程，`end` 层在多个阶段可见），用容器分组表达不了。改为**控制器持有阶段表**：

```
[Serializable] class PhaseEntry {
    string  id;              // launch | travel | sustain | impact | end（原型声明的子集）
    float   duration;        // < 0 表示外部驱动 / 等待入事件
    bool    loop;            // 仅 sustain
    int[]   activeLayers;    // 该阶段激活的层索引
    float[] layerOffsets;    // 各层相对阶段起点的延迟
    float[] layerDurations;  // 各层时长（< 0 = 跟随阶段）
}
```

### 5.2 任意阶段集合

- 阶段表由 recipe 的 `timing.phases` + 原型声明生成，**长度 0~5，顺序按标准序**（`launch → travel|sustain → impact → end`），缺失的阶段不出现在表里。
- `CurrentPhaseIndex` 是 `int`（−1 = 未播放），不是枚举——枚举会把阶段集合再次硬编码。
- 阶段推进：`autoAdvance == true` 时 `duration` 到期自动进下一阶段；`false` 时停在当前阶段等待入事件。
- `complete` 不是阶段，是"最后一个阶段结束 + 全部层清空"后发出的出事件（沿用 v1 的 `IsAlive` 判定思路，扩展到 VisualEffect 的 `aliveParticleCount` 与刚体的 `IsSleeping`）。

### 5.3 `sustain` 循环

- `sustain` 阶段的 `duration < 0` 且 `loop == true` 时无限循环，直到收到 `end` 入事件或 `maxSustainSeconds` 超时（默认 300 s 的安全阀，防止用户忘记调 `end` 导致特效永驻）。
- 循环内材质的 `_Progress` 在 [0,1) 内周期推进（`frac(elapsed / cycleDuration)`），粒子系统持续发射，`VfxLightBeat` 的相位连续。
- 超时触发时发 `onSustainTimeout` 出事件并自动进入 `end`（**不静默结束**）。

### 5.4 事件

**入事件（inbound）分派**：

```
bool SendEvent(string eventId, in VfxEventPayload payload)
```

- 标准入事件：`launch / travel / sustain / impact / end`（阶段推进）。
- 原型特有入事件：`hitAt(point, normal) / break() / setIntegrity(float) / setNodes(Vector3[]) / addNode(Vector3) / setProgress(float) / setTarget(Transform) / setTravelPose(pos, rot) / release() / consume(index)` 等，由原型定义。
- **分派机制**：编译期生成一张 `eventId → handlerIndex` 的**排序字符串数组 + 并行处理器索引数组**，运行时用二分查找定位（`O(log n)`，n ≤ 16，零分配、零反射、零字典哈希）。处理器是编译期烘死的枚举分支（与 §6 的绑定处理器同一机制）。
- 未知 `eventId` 返回 `false`，**不抛异常**（用户代码探测能力的常见模式）。
- `interface.events.expose` 之外的事件即使原型支持也返回 `false`（接口显式声明纪律，`RECIPE_V2_SCHEMA_DRAFT.md` §1-4）。

**出事件（outbound）双形式**：

```
public event Action<VfxEventPayload> OnImpact;      // C# event：零 GC、代码订阅
[SerializeField] UnityEvent<VfxEventPayload> onImpactUnity;   // UnityEvent：Inspector 连线
```

- 编译期为 `interface.events.emit` 中的每个出事件生成**两个成员**。两者在同一处触发（一个私有 `Raise(index, payload)` 方法遍历两个数组）。
- 标准出事件：`onLaunch / onTravel / onSustain / onImpact / onEnd / onComplete`；原型特有：`onHop / onApex / onBreak / onHitRipple / onSettled / onRelease / onDissolved / onSustainTimeout` 等。
- **出事件的 payload 是 struct**（`VfxEventPayload { Vector3 position; Vector3 normal; int index; float value; }`），不是 class——高频事件（如 `onHop`）不产生 GC。

### 5.5 池化复位

```
void ResetForPool()
```

必须做的 9 件事（缺一即谓词 RT-6 FAIL）：

1. 全部 `ParticleSystem.Stop(true, StopEmittingAndClear)` + `Clear(true)`。
2. 全部 `TrailRenderer.Clear()` / `LineRenderer.positionCount = 0`。
3. 全部 `VisualEffect.Reinit()`。
4. 全部碎块 `Rigidbody.isKinematic = true`、`velocity = zero`、`angularVelocity = zero`，并复位到初始局部 TRS（初始 TRS 在编译期烘进一个 `Vector3[]/Quaternion[]` 数组，不是运行时 `GetChild` 遍历）。
5. `Cloth.ClearTransformMotion()` + 复位。
6. 全部材质的 `_Progress = 0`、`_LocalTime = 0`、`_Phase = 0`（通过共享的 `MaterialPropertyBlock`）。
7. `VfxLightBeat` 相位归零；`Light.intensity` 归到基准。
8. `CurrentPhaseIndex = -1`；阶段计时器归零；`sustain` 超时计时器归零。
9. 根 Transform 复位到初始局部 TRS（**局部，不是世界**——池化时对象可能挂在不同父节点下）。

复位后 `IsAlive == false` 且下一次 `SendEvent("launch")` 的表现与首次实例化完全一致（谓词 RT-7 的可断言形式：复位后再播放，第 N 帧的 `MaterialPropertyBlock` 值与首次播放的第 N 帧相同）。

### 5.6 外部引用为空时的退化

`interface.parameters` 中类型为 `transformRef` / `rendererRef` 的参数（如作用目标、挂点、连接终点）在用户未赋值时，**必须有确定的退化行为，不得空引用异常，不得静默不播**：

| 参数语义 | 退化规则 |
|---|---|
| `targetRenderer`（消散类作用于目标渲染器） | 退化为使用预制体自带的**几何壳占位**（一个由 `shell_polyhedron` 生成的低细分球/盒），尺寸取 `scale` 参数。特效仍完整播放，只是作用在占位体上 |
| `targetTransform`（连接终点 / 追踪目标） | 退化为"根节点前方 `defaultDistance` 米处的固定点"（`defaultDistance` 由 recipe 给，默认 3 m）。链接类特效变成"从根伸向前方的定长链接" |
| `attachPoint`（挂点） | 退化为根节点自身 |
| `groundPlane`（贴地参考） | 退化为 `y = 根节点 y`（3D）/ `y = 根节点 y`（2D），不做射线检测（射线检测对场景做假设） |
| `clothColliders` | 退化为空数组（布料只受重力与风） |
| `impactPoint` | 退化为根节点位置 |

**统一纪律**：退化必须在 `Awake` 时完成并写入内部字段，运行时不再逐帧判空。谓词 RT-5 断言每个 `*Ref` 参数在参数块中都有对应的 `fallbackMode` 声明。

### 5.7 参数块与绑定表

```
VfxParameterBlock（预制体根）
├─ 标准参数：palette(Gradient) / intensity(float) / scale(float) / speed(float) / seed(uint)
├─ 原型特有参数：按 interface.parameters 生成的序列化字段（类型见 schema 的 10 值）
├─ 绑定表：BindingEntry[] { paramIndex, targetIndex, bindingKeyId, mapKind, mapArgs }
└─ 目标数组：Renderer[] / ParticleSystem[] / VisualEffect[] / Component[]（lightTargets）/ Transform[]
```

- **参数值改变 → 立即应用**：`VfxParameterBlock` 提供 `SetFloat(id, v)` 等 API，内部标脏并在 `LateUpdate` 批量应用（避免同一帧多次 set 触发多次写材质）。Inspector 修改经 `OnValidate` 走同一路径。
- **标准参数自动绑定到全部层**（`RECIPE_V2_SCHEMA_DRAFT.md` §5），不需要在 recipe 里声明。
- **绑定表在编译期生成，运行时只读**。`bindingKeyId` 是 §6 的键的整数化 id（编译期解析字符串键为索引，运行时零字符串比较）。
- **关闭的层的绑定为空操作**：档位降级关掉的层，其绑定条目的 `targetIndex = -1`，应用时跳过。这保证同一 recipe 六档产物的**接口完全一致**（`RECIPE_V2_SCHEMA_DRAFT.md` §7-5）。

### 5.8 标准参数与原型特有参数的暴露方式

| | 标准参数 | 原型特有参数 |
|---|---|---|
| **Inspector** | `VfxParameterBlock` 顶部固定 5 项，自定义 Editor 绘制（Gradient 用 HDR 模式） | 下方按 `interface.parameters` 顺序绘制；`range` 生成 Slider，`enumValues` 生成 Popup，`transformRef/rendererRef` 生成对象槽 + "未赋值时的退化说明"提示 |
| **代码（强类型）** | `controller.Intensity`、`controller.Scale`、`controller.Speed`、`controller.Seed`、`controller.Palette` 属性 | **无强类型属性**（recipe 决定的参数集无法在编译期生成 C# 属性而不引入代码生成）。改为 `controller.Parameters.SetFloat("integrity", 0.5f)` / `GetFloat` / `SetEnum` / `SetTransform` 等类型化 API + 字符串 id |
| **代码（零分配路径）** | 同上 | `int handle = controller.Parameters.Resolve("integrity")`（一次，缓存）→ `controller.Parameters.SetFloat(handle, v)`（零字符串比较） |
| **Recipe** | 隐式（不可重声明，schema 已断言） | `interface.parameters[]` |

**为什么不做代码生成**：v2 的产物是 prefab，不是 C# 类。为每个 recipe 生成一个 partial class 会把编译产物扩散到 `Assets/Scripts/`——越过 ADR-007 的写入面。`Resolve → handle` 的两段式是标准的零分配折中。

### 5.9 编排的内联展开（ADR-010 §4bis-3）

编排 recipe 编译时，子 recipe **在编译期展开合并**成单一预制体：

- 每个 `children[i]` 的层被内联为 `Layers/<childId>__<layerId>`（层 id 加前缀避免冲突）。
- 子实例的控制器**不生成**；其阶段表被合并进父控制器的阶段表（按 `timeline` 的 `t` 与 `waitFor` 展开为父阶段内的 `layerOffsets`）。
- `wiring` 的 `from → to` 在编译期解析：若两端都在同一预制体内，转为父控制器的**内部事件路由表**（`sourceEventIndex → targetHandlerIndex`），运行时零字符串。
- `anchor: follow:<childId>` 转为父控制器每帧把该子实例的层节点位置写到跟随者的层节点（编译期烘死索引对）。
- 成本模型对内联后的**合并产物**计算，不是逐子实例（`PROTOTYPE_CATALOG_v1.md` §10.2-9 的"总预算超限按 priority 禁用"在合并后执行）。
- `recipeRef` 引用的子 recipe 的 `sha256` 记入产物 manifest（依赖溯源）；子 recipe 变了 → 父的 buildHash 变 → DryRun 报 `Update`。

schema 的 `children[].recipeRef` 保留"链接"语义的字段形状但 v1 不实现（ADR-010 §4bis-3）。

---

## 6. 绑定 allow-list v2

### 6.0 v1 的问题与要保留的内核

**要保留的内核**（`VfxBindingHandlerRegistry.cs` 的核心价值）：

> Recipe data is never interpreted as a type or property path. 显式键 → 显式处理器，禁反射；未登记的键抛异常。

这条纪律必须原样保留：recipe 是不可信输入（可能来自 AI），它绝不能变成"往哪个组件的哪个字段写什么"的指令。

**v1 的僵化**：22 个常量 × 22 个 lambda，每加一个参数要改两个文件。v2 有 5 族 × 数十个变体 × 每变体十余参数，按 v1 的模式会有上千个常量——不可维护，且每次加变体都要改注册表（正是用户点名要避免的）。

### 6.1 v2 的键结构：三段式

```
<family>.<target>.<property>
```

| 段 | 取值 | 说明 |
|---|---|---|
| `family` | `mat` / `gpu` / `cpu` / `mesh` / `light` / `ctrl` | 5 族 + 控制器 |
| `target` | 族内的写入目标类别（见 §6.2 各表） | 决定"往哪类组件写" |
| `property` | 具体属性 | 决定"写哪个字段" |

例：`mat.prop.float`、`cpu.main.startLifetime`、`light.beat.flickerDepth`、`ctrl.phase.duration`。

### 6.2 按族分表

**`mat.*`（材质族）—— 关键设计：材质属性是数据驱动的，不需要逐属性登记**

| 键 | 处理器 | 值类型 | 说明 |
|---|---|---|---|
| `mat.prop.float` | `mpb.SetFloat(nameId, v)` | float | `nameId` 来自**绑定条目的第二个参数**（编译期由变体清单解析出的 `Shader.PropertyToID`），不是来自 recipe |
| `mat.prop.vector` | `mpb.SetVector` | Vector4 | 同 |
| `mat.prop.color` | `mpb.SetColor` | Color | 同 |
| `mat.prop.int` | `mpb.SetInt` | int | 同 |
| `mat.keyword` | `material.EnableKeyword/DisableKeyword` | bool | 关键字名同样来自变体清单 |
| `mat.renderer.sortingOrder` | `renderer.sortingOrder = v` | int | 2D |
| `mat.renderer.enabled` | `renderer.enabled = v` | bool | |

**这解决了僵化问题**：材质族的成百上千个 shader 属性由 **7 个键**覆盖。安全性没有降低——`nameId` 不是 recipe 提供的字符串，而是**编译期从变体清单（人工维护的白名单资产）解析出来的**。recipe 只能说"把参数 X 绑到层 L 的 `material.crackProgress`"，编译器去变体清单查 `crackProgress` 是否是该变体声明的参数、类型是否匹配，命中才生成一条 `(mat.prop.float, nameId)` 的绑定条目。**recipe 的字符串从不到达运行时**。

**`gpu.*`（GPU 粒子族）**

| 键 | 处理器 | 值类型 |
|---|---|---|
| `gpu.exposed.float` / `.vector` / `.int` / `.uint` / `.bool` / `.gradient` / `.curve` | `VisualEffect.SetFloat(nameId, v)` 等 | 对应类型；`nameId` 同样来自模板清单 |
| `gpu.playRate` | `visualEffect.playRate = v` | float |
| `gpu.enabled` | `visualEffect.enabled = v` | bool |
| `gpu.sendEvent` | `visualEffect.SendEvent(eventNameId)` | trigger |

**`cpu.*`（CPU 粒子族）—— 这里必须逐属性登记**

ParticleSystem 的模块是 struct 属性，没有 `SetFloat(nameId)` 式的通用入口，因此这一族无法数据驱动，只能显式枚举。按模块分组：

| 组 | 键（每组列举代表，完整表由 T3 落地时按本规则补全） |
|---|---|
| `cpu.main.*` | `startLifetime` / `startSpeed` / `startSize` / `startColor` / `startRotation` / `gravityModifier` / `maxParticles` / `simulationSpeed` |
| `cpu.emission.*` | `rateOverTime` / `rateOverDistance` / `burstCount` / `burstCycles` / `burstInterval` |
| `cpu.shape.*` | `radius` / `angle` / `arc` / `scale` / `randomDirectionAmount` |
| `cpu.velocity.*` | `linearX/Y/Z` / `orbitalX/Y/Z` / `radial` / `speedModifier` |
| `cpu.limit.*` | `limit` / `dampen` / `drag` |
| `cpu.force.*` | `x/y/z` |
| `cpu.noise.*` | `strength` / `frequency` / `scrollSpeed` / `damping` |
| `cpu.color.*` | `gradient` |
| `cpu.size.*` | `curve` / `sizeMultiplier` |
| `cpu.rotation.*` | `angularVelocity` |
| `cpu.collision.*` | `bounce` / `dampen` / `lifetimeLoss` / `radiusScale` |
| `cpu.trails.*` | `ratio` / `lifetime` / `widthOverTrail` / `colorOverLifetime` |
| `cpu.renderer.*` | `sortingOrder` / `lengthScale` / `velocityScale` / `enabled` |

约 **50 个键**。这是不可避免的（Unity API 形状决定），但它是**有界的**：ParticleSystem 的模块字段集合是 Unity 固定的，不随我们新增变体而增长。加一个新的 CPU 变体不需要改注册表——只要它用的字段已在这 50 个内。**这正是与 v1 的关键差别**：v1 的键是"模板 × 参数"（随内容增长），v2 的键是"Unity API 字段"（随引擎版本增长，即近似不变）。

**`mesh.*`（网格族）**

| 键 | 处理器 | 值类型 |
|---|---|---|
| `mesh.transform.localScale` / `.localPosition` / `.localRotation` | Transform 写入 | Vector3 / Quaternion |
| `mesh.renderer.enabled` / `.sortingOrder` / `.shadowCasting` | Renderer 写入 | bool / int / enum |
| `mesh.trail.time` / `.widthMultiplier` / `.colorGradient` / `.emitting` | TrailRenderer | float / Gradient / bool |
| `mesh.line.positionCount` / `.setPositions` / `.widthMultiplier` / `.colorGradient` | LineRenderer | int / Vector3[] / … |
| `mesh.cloth.externalAcceleration` / `.randomAcceleration` / `.damping` / `.stretchingStiffness` | Cloth | Vector3 / float |
| `mesh.debris.breakForce` / `.explode` / `.gravityScale` / `.fadeMode` | `VfxDebrisDriver` / Rigidbody 组 | float / trigger / enum |

约 **20 个键**。网格族的运行时可写面本就窄（几何在编译期定死，运行时只改变换与组件参数）。

**`light.*`（局部光族）**

全部写 `VfxLightBeat` 的字段（**不直接写 `Light`/`Light2D`**——那会与节拍器的每帧写入打架）：

| 键 | 字段 |
|---|---|
| `light.beat.color` / `.intensity` / `.range` / `.innerAngle` / `.outerAngle` | 基础 |
| `light.beat.flickerMode` / `.flickerRate` / `.flickerDepth` / `.decayShape` | 节拍 |
| `light.beat.intensitySteps` / `.flickerQuantize` / `.beatFrameRate` | 风格量化 |
| `light.beat.castShadows` / `.enabled` / `.phaseOffset` | 其他 |

**15 个键**。

**`ctrl.*`（控制器）**

| 键 | 处理器 |
|---|---|
| `ctrl.phase.duration` | 阶段表条目的 duration（索引来自绑定条目） |
| `ctrl.phase.autoAdvance` | bool |
| `ctrl.layer.offset` / `.duration` / `.enabled` | 层时序 |
| `ctrl.speed` | 全局时间倍率 |
| `ctrl.custom.float` / `.int` / `.bool` / `.enum` / `.vector3` / `.transform` | 原型特有参数写入控制器的 `customValues[]`（索引来自绑定条目），供该原型的逻辑读取（如 `hopInterval` / `topology` / `maxSegments`） |

约 **11 个键**。`ctrl.custom.*` 是原型逻辑的通用通道：控制器不知道 `hopInterval` 是什么，它只知道 `customValues[3]` 被写了，原型逻辑（编译期选定的一个枚举分支）去读它。

### 6.3 总键数与增长性

| 族 | 键数 | 随什么增长 |
|---|---|---|
| `mat` | 7 | **不增长**（数据驱动） |
| `gpu` | 9 | **不增长**（数据驱动） |
| `cpu` | ~50 | Unity ParticleSystem API |
| `mesh` | ~20 | Unity 组件 API |
| `light` | 15 | `VfxLightBeat` 字段 |
| `ctrl` | 11 | 控制器能力 |
| **合计** | **~112** | **不随变体 / 原型 / 元素 / 风格增长** |

这是本节的核心结论：**加一个材质子图、一个 VFX 模板、一个网格生成器、一个元素、一个风格、一个原型，都不需要动绑定注册表**。只有"Unity 又开放了一个我们要写的组件字段"才需要加键，那是低频事件且需要人工评审——正合 allow-list 的本意。

### 6.4 处理器的实现约束（禁反射的具体化）

```
public delegate void Handler(in BindingContext ctx, in BindingValue value);

readonly struct BindingContext {
    public readonly Component Target;     // 编译期解析的目标组件
    public readonly int       NameId;     // 编译期解析的属性 id（Shader.PropertyToID / VFX property id）
    public readonly int       SlotIndex;  // 阶段/层/custom 的索引
    public readonly MaterialPropertyBlock Mpb;
}
```

- 处理器是**静态方法**，注册在一个按 `keyId` 索引的数组里（不是 Dictionary——keyId 是编译期分配的稠密整数）。
- `BindingValue` 是 struct union（`float/int/Vector4/Color` 共存），避免装箱。
- **零反射**：无 `GetProperty` / `SetValue` / `Type.GetType` / `Invoke`。谓词 BD-3 用源码级扫描断言（与 `VfxOutputAuditor` 扫描禁止组件类型同构）。
- **未登记 keyId 在编译期就不可能出现**（keyId 由编译器分配，不来自 recipe）。运行时的 `Apply` 对越界索引抛异常作为最后防线。

### 6.5 编译期的绑定解析（recipe 字符串在哪里被消化）

```
recipe: { "layerId": "cracks", "path": "material.crackProgress", "map": "invert" }
   ↓ ① layerId → 层索引（层表查找；不存在 → E207）
   ↓ ② path 前缀 "material." → family = mat
   ↓ ③ "crackProgress" → 查该层 technique.variant 的变体清单参数表
   ↓    不存在 → E207；类型不匹配 → E207
   ↓ ④ 变体清单给出：shaderPropertyName = "_CrackProgress", type = float
   ↓ ⑤ 生成绑定条目：{ keyId = KeyId(mat.prop.float),
                       targetIndex = 层的 Renderer 索引,
                       nameId = Shader.PropertyToID("_CrackProgress"),
                       mapKind = Invert }
   ↓ ⑥ recipe 的字符串到此为止，不进入产物
```

第 ⑥ 步是安全性的关键：产物中不存在任何来自 recipe 的属性路径字符串。谓词 BD-4 断言产物的绑定表中零个 string 字段。

---

## 7. 门禁谓词清单

### 7.1 方法论继承（ADR-009）

三条继承：**按族的机器可查视觉谓词**（不是美学判断）、**fail-closed**（未声明者拒绝）、**显式豁免**（不可静默扩缩）。断言面是资产序列化状态 + sidecar 清单，EditMode 可判定，不进 PlayMode、不渲染。谓词编号稳定，失败信息必须引用编号。

### 7.2 各族谓词的所在

| 族 | 谓词 | 数 | 文档 |
|---|---|---|---|
| 材质 | SG-1~8 / MG-1~5 / MV-1~9 / GL-1~7 / PR-1~2 | 31 | `TECH_FAMILY_SPEC_MATERIAL.md` §12 |
| GPU 粒子 | GP-1~9 | 9 | `TECH_FAMILY_SPEC_PARTICLES.md` §9.1 |
| CPU 粒子 | CP-1~10 | 10 | 同 §9.2 |
| 伪粒子（UGUI） | UI-1~5 | 5 | 同 §9.3 |
| 网格 | MS-1~12 | 12 | `TECH_FAMILY_SPEC_MESH_LIGHT.md` §8.1 |
| 物理 | PH-1~9 | 9 | 同 §8.2 |
| 局部光 | LT-1~9 | 9 | 同 §8.3 |
| 风格 | ST-1~6 / CT-1~8 / PX-1~10 | 24 | `STYLE_IMPL_CARTOON_PIXEL.md` §7 |
| **编译器侧（本节）** | WL-1~6 / CM-1~6 / RT-1~9 / BD-1~5 / AU-1~6 | 32 | §7.3 |
| **折射可选层（本文档 §9.5）** | RF-1~5 | 5 | §9.5 |
| 画廊场景 | GA-1~10 | 10 | `GALLERY_SPEC.md` §8 |
| **合计** | | **156** | 每族均 ≥ 3 条（材质 31 / GPU 9 / CPU 15 含 UI / 网格 21 含物理 / 光 9） |

### 7.3 编译器侧谓词

**白名单（WL-*）**

| 编号 | 谓词 |
|---|---|
| WL-1 | `AssetAllowList.json` 的每一项解析到存在的资产且 `sha256` 匹配 |
| WL-2 | `Assets/VFX/Shared/{Shaders,Subgraphs,VfxTemplates}/` 下每个资产都在清单内（**双向**，堵死"合法路径下的未登记新资产"） |
| WL-3 | 每个网格生成器的 `codeSha256` 与源文件哈希匹配；若源变了而 `version` 未变 → FAIL |
| WL-4 | 产物中每个材质的 shader GUID、每个 `VisualEffect` 的 asset GUID、每个 `StyleStage` 子图 GUID 命中白名单 |
| WL-5 | `AssetDatabase.GetDependencies(prefab, true)` 的每一项落在 `allowedDependencyRoots` 内（沿用 `E601` / E8013 语义） |
| WL-6 | 白名单清单文件本身不被构建流程写入（读取时记录 mtime，构建后比对；ADR-007 §2.1：`ProjectSettings/**` 其余路径不可写） |

**成本模型（CM-*）**

| 编号 | 谓词 |
|---|---|
| CM-1 | 产物的实测八项成本（从组件与资产直接量取）与编译报告声明的值完全一致（**报告不能撒谎**） |
| CM-2 | 八项全部 ≤ §4.2 的档位上限（`C_over` 用硬上限 = 上限×1.5） |
| CM-3 | 分项结构预算（GameObject / 深度 / Material / Texture / Shader）达标 |
| CM-4 | **六档接口一致性**：同一 recipe 的六档产物，其 `VfxParameterBlock` 的参数 id 列表、类型、默认值与控制器的事件表**逐字段相同**（`RECIPE_V2_SCHEMA_DRAFT.md` §7-5）。这是"用户切换档位无需改代码"的机器证明 |
| CM-5 | 降级登记完备：编译报告中每一处族替换 / 参数截断 / 层关闭都有一条 `W404`，且产物状态与登记一致 |
| CM-6 | **确定性**：同一 `(recipe, catalogVersions, tier)` 连续编译两次，产物 prefab 与全部生成资产的 sha256 相同 |

**运行时控制器（RT-*）**

| 编号 | 谓词 |
|---|---|
| RT-1 | 预制体根恰好一个 `IVfxRuntimeEntry`（沿用 R8020）且类型为 `VfxController` |
| RT-2 | 阶段表的 `id` 全部属于该原型声明的阶段集合；顺序为标准序；`loop` 只出现在 `sustain` |
| RT-3 | 每个原型的**必需层**在阶段表中至少被一个阶段激活（否则该层永不出现，等于缺失） |
| RT-4 | 事件表：`expose` 集合 ⊆ 原型标准入事件 + 原型特有入事件；`emit` 同理；分派数组已排序（二分查找的前提） |
| RT-5 | 每个 `transformRef` / `rendererRef` 参数在参数块中有 `fallbackMode` 声明（§5.6） |
| RT-6 | `ResetForPool` 的 9 项复位目标数组全部非空（有该类组件时）：例如产物含 `VisualEffect` 则复位数组必须含它 |
| RT-7 | 控制器不引用预制体外的任何对象（全部序列化引用的 `GetInstanceID` 归属该 prefab 层次） |
| RT-8 | `maxSustainSeconds > 0`（安全阀存在） |
| RT-9 | 编排产物：`children` 已内联（产物中零个嵌套 `VfxController`）；内部事件路由表的每个端点索引有效 |

**绑定（BD-*）**

| 编号 | 谓词 |
|---|---|
| BD-1 | 绑定表的每个 `keyId` 在 §6.2 的键表内（**未登记键拒绝**，v1 `E500` 语义的延续） |
| BD-2 | 每个绑定条目的 `targetIndex` 有效（或 = −1 表示层已关闭）；`nameId` 对应的属性在该层的变体清单中已声明 |
| BD-3 | **零反射**：`VFXComposer.Runtime` 程序集内零个 `System.Reflection` 引用（源码级扫描 + 程序集引用检查） |
| BD-4 | 产物的绑定表中零个 string 字段（§6.5-⑥） |
| BD-5 | 标准参数（`palette/intensity/scale/speed/seed`）的绑定覆盖全部启用层 |

**审计器（AU-*，v1 保留项的 v2 复述）**

| 编号 | 谓词 |
|---|---|
| AU-1 | 产物内组件类型 ⊆ §2.2 闭集 + `Transform`；§2.3 排除表零命中 |
| AU-2 | 无缺失 MonoBehaviour 脚本；无 Editor 程序集组件；无 `forbiddenRuntimeComponentTypeNames` 成员（沿用 E8008/E8009/E8010） |
| AU-3 | GameObject 名称在产物内唯一（沿用 R8007）；无 `forbiddenProductionNameTokens`（R8017/R8018） |
| AU-4 | 每个 Renderer 有非空材质且材质有非空 shader（沿用 E8005/E8006） |
| AU-5 | 产物目录内无不可达资产（沿用 R8015 的 stale 检查） |
| AU-6 | 产物内零 Shader 资产（沿用 R8016） |

### 7.4 fail-closed 三路（统一定义）

| 路 | 条件 | 码 | 语义 |
|---|---|---|---|
| **1. 未立法族** | `technique.family` 不在 5 族闭集；或某族的谓词集合为空（族被新增但未立法谓词） | `E203` / 门禁 FAIL | 与 ADR-009 §5-1 的"未立法 kind 拒绝"同构。**新增技术族必须先修订 ADR-010 §4 与本文档补谓词**（MUST 级变更） |
| **2. 无变体声明** | `technique.variant` 不在该族的变体清单；或变体清单缺失 `dimensions` / `minTier` / `sampleCost` 声明；或子图/模板/生成器无 sidecar 清单 | `E203` / SG-1 / GP-1 / MS-1 | 与 ADR-009 §5-2 的"无 manifest 拒绝"同构 |
| **3. 资产不在白名单** | 直接引用的 GUID 未命中 `AssetAllowList`；或依赖闭包越界 | `E401` / `E601` / WL-4 / WL-5 | v1 `E601` 的强化版 |

三路的共同语义：**拒绝 = 零写入或已回滚**（ADR-007 §5 总原则）。前两路在 DryRun 阶段拦截（零写入）；第三路的闭包检查在临时目录阶段（已构建但未提交，走回滚序列）。

### 7.5 显式豁免机制

与 ADR-009 §4 逐条同构：

1. 豁免表是**测试内显式常量表**：`(谓词编号, 资产/变体 id) → (豁免理由, 到期卡号)`。
2. 豁免项**仍然跑谓词**：若已达标，测试失败并要求把它从豁免表移除（防豁免表陈旧）。
3. 测试最终断言**已消费豁免集合恰等于声明清单**（不可静默扩、不可静默缩）。
4. 豁免表内的 id 必须真实存在（幽灵豁免即失败）。
5. **豁免不适用于 fail-closed 三路**：未立法族 / 无变体声明 / 资产不在白名单**不可豁免**——那三条是边界本身，豁免它们等于取消边界。豁免只适用于"某个具体资产暂未达到某条质量谓词"的存量处置。

---

## 8. 三件套写入面是否需要修订：**结论 = 不修订**

### 8.1 ADR-007 §2.1 的三个成员

1. 资产产物唯一根 `Assets/VFX/Generated/**`
2. 审计元数据单点 `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json`
3. 构建溯源单文件 `Assets/VFX/Recipes/<Sanitize(effectId)>.json`

### 8.2 v2 新增的写入需求逐项核对

| v2 新增产物 | 落点 | 是否越界 |
|---|---|---|
| 生成网格 `.asset` | `Assets/VFX/Generated/<id>/Meshes/*.asset` | 成员 1 内 |
| 网格 sidecar `.mesh.json` | `Assets/VFX/Generated/<id>/Meshes/*.mesh.json` | 成员 1 内 |
| 克隆材质 | `Assets/VFX/Generated/<id>/*.mat` | 成员 1 内（v1 已如此） |
| 生成的 1×1 白 sprite（2D `SpriteRenderer` 用） | `Assets/VFX/Generated/<id>/*.asset` | 成员 1 内 |
| 六档产物（同一 recipe 最多 6 个 prefab） | `Assets/VFX/Generated/<id>/<id>__<tier>.prefab` | 成员 1 内（目录内多文件，不是多根） |
| 编译报告 | `Assets/VFX/Generated/<id>/BuildReport.json` | 成员 1 内（v1 的 `BuildManifest.json` 同位置） |
| 产物所有权 manifest | `ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json` | 成员 2 |
| recipe 溯源 | `Assets/VFX/Recipes/<id>.json` | 成员 3 |
| **子图库 / 模板库 / 生成器代码 / 白名单清单 / StylePreset / ElementPreset** | `Assets/VFX/Shared/**` 与 `ProjectSettings/VFXComposer/AssetAllowList.json` | **不写**——人工维护的预置资产（ADR-007 §2.2 的 Shared 只读政策） |

### 8.3 结论与理由

**三件套写入面无需修订。** 三条理由：

1. **v2 的全部新增产物都落在成员 1 之内**。产物种类变多（网格、sprite、六档 prefab、sidecar），但它们共享同一个根目录与同一套原子提交/回滚序列。ADR-007 §2.1 的成员 1 定义的是**根**，不是文件类型清单，因此不需要修订。
2. **v2 新增的"共享资产"全部是只读依赖，不是写入面**。子图库、模板库、白名单是人工预置的，AI 构建期只读——这与 ADR-007 §2.2 对 `Assets/VFX/Shared/Shaders` 的定性逐字一致（"仓库预置源资产，`Ensure()` 只经 `Shader.Find` 引用、缺失即抛异常，不写入"）。v2 把这个模式从"一个 shader 目录"扩展到"整个共享资产库"，**方向与 ADR-007 一致而不是相反**。
3. **修订会削弱边界**。若为了容纳"编译器可以补建缺失子图"而扩写入面，就重演了 ADR-007 §3.2 否决的"幂等补齐"方案：一次 AI 动作可静默改写共享视觉。ADR-007 已经论证过这条路的问题（S1 隐式传播风险 / "存在但内容错误"是最难排查的故障面 / Shared 终态取决于构建历史顺序），v2 没有提供任何新论据。

**需要在 T3 落地时注意的一处衔接**（不是 ADR 修订，是实现细节）：ADR-007 §2.1 成员 1 的孤儿临时目录清扫前缀枚举清单（`vfxs6tmp_`、`impacttmp_`、`areatmp_`）需要登记 v2 编译器的临时前缀（建议 `vfxv2tmp_`）。ADR-007 §2.4 明文说该清单"随新编译器登记扩展"，因此这是清单登记，不是决策修订。

---

## 9. 折射可选层的编译期处理

### 9.1 问题

同一个 recipe 在两种 URP 配置（`Opaque Texture` 开 / 关）下有两种表现（`TECH_FAMILY_SPEC_MATERIAL.md` §6）。编译器必须诚实声明这一点，且不能因为编译机的配置而产出"只能在一种配置下工作"的产物。

### 9.2 编译期动作

1. **不做配置剪裁**：材质的 shader 用 `multi_compile_local _ _REFRACT_SCENECOLOR`（不是 `shader_feature`），保证两个变体都进 Player 构建。谓词 MV-8 断言。
2. **关键字初值** = 编译机检测结果（只是初值；`VfxUrpCapabilityProbe` 在 `Awake` 覆盖）。
3. **`VfxUrpCapabilityProbe` 组件**在产物根生成，其 `refractionTargets` 数组烘死了受影响的材质索引。
4. **编译报告声明变体差异**（§9.3）。
5. **成本模型按"真折射路"计算**（较高的那一条：多 1 次纹理采样）。保守估计，不因运行时可能走降级路而低估。

### 9.3 编译报告中的变体差异声明

```jsonc
"conditionalRendering": {
  "refraction": {
    "affectedLayers": ["core", "surface"],
    "routes": [
      { "id": "sceneColor", "requires": "URP Opaque Texture enabled",
        "visual": "true background refraction via _CameraOpaqueTexture",
        "cost": { "textureSamples": 1 } },
      { "id": "normalPerturb", "requires": "none (always available)",
        "visual": "normal-perturbed specular highlight; no background displacement",
        "cost": { "textureSamples": 0 } }
    ],
    "compileTimeDetected": "sceneColor",
    "runtimeSelected": "determined at Awake by VfxUrpCapabilityProbe"
  },
  "depthFade": {
    "affectedLayers": ["body", "veil"],
    "resolution": "compile-time",           // 与折射不同：影响几何形状，不能运行时切换
    "compileTimeDetected": "depthTextureEnabled",
    "appliedRoute": "softDepthFade",
    "note": "若目标运行环境的 Depth Texture 关闭，需以 depthTexture=off 重新编译以获得 domeBillboard 降级几何"
  }
}
```

### 9.4 两条可选层的处理差异（必须区分）

| | 折射（`Opaque Texture`） | 深度软化（`Depth Texture`） |
|---|---|---|
| 影响什么 | 只影响 shader 内的采样与混合 | 影响 `Glow_k` 的**几何形状**（quad vs domeBillboard，§`TECH_FAMILY_SPEC_MATERIAL.md` §5.7） |
| 能否运行时切换 | **能**（关键字切换） | **不能**（几何是编译期资产） |
| 处理 | 双变体 + 运行时检测 | 编译期按 `PLATFORM_PROFILE` 定死 + 报告声明 + 建议重编译 |
| 两条路都不报错 | 是（ADR-010 §4bis-1） | 是（降级路仍成立，只是不是最优） |

### 9.5 谓词

| 编号 | 谓词 |
|---|---|
| RF-1 | 声明了折射的层，其材质 shader 的 `_REFRACT_SCENECOLOR` 是 `multi_compile_local`（非 `shader_feature`） |
| RF-2 | 含折射层的产物根必有 `VfxUrpCapabilityProbe`，且 `refractionTargets` 非空且全部在本预制体内 |
| RF-3 | 编译报告的 `conditionalRendering.refraction.affectedLayers` 与产物中实际启用折射的层集合完全一致 |
| RF-4 | 折射层的渲染队列偏移 < 其 `Glow_k` 的偏移（`TECH_FAMILY_SPEC_MATERIAL.md` §6.4 的 Queue 表） |
| RF-5 | 2D 产物中零个层启用 `_REFRACT_SCENECOLOR` 初值（2D 恒走降级路，同上文档 §11） |

---

## 10. 错误码汇总（`RECIPE_V2_SCHEMA_DRAFT.md` §10 的补充）

沿用草案的 E100~E301 / W400~W401，本文档新增：

| 码 | 含义 |
|---|---|
| `E401` | 直接引用的资产不在 `AssetAllowList`（白名单未准入） |
| `E402` | 白名单清单自身损坏（sha256 不匹配 / 引用不存在的资产） |
| `E403` | 变体清单缺失或声明不完整（无 `dimensions` / `minTier` / `sampleCost`） |
| `E404` | 子图 / 模板 / 生成器缺 sidecar 清单 |
| `E302` | 分项结构预算超限（GameObject / 深度 / Material / Texture / Shader） |
| `E500` | 绑定键不在 §6.2 的键表（v1 同码语义延续） |
| `E501` | 绑定应用失败（v1 同码语义延续） |
| `E502` | 绑定目标解析失败（层不存在 / 组件不存在 / 属性未在变体清单声明） |
| `E603` | 产物组件类型在闭集外（AU-1） |
| `E604` | 门禁谓词失败（附谓词编号，如 `E604 [MV-4]`） |
| `W403` | `C_over` 在 [上限, 上限×1.5) 的警告区 |
| `W404` | 自动降级已应用（每次一条） |
| `W405` | 条件渲染路由差异（折射 / 深度软化，§9.3） |
| `W406` | 参数在当前实现路径下无对应项被忽略（如伪粒子的碰撞参数，`TECH_FAMILY_SPEC_PARTICLES.md` §8.4） |

---

## 11. 审计清单

- [ ] v1 边界 10 条事实逐条核实并给出 v2 处置（保留 / 扩展 / 推翻）。
- [ ] 识别组件闭集 18 类 + 6 个运行时脚本，每类有写入面纪律；排除表含理由（尤其 `Animator` 与序列帧禁令的同源性）。
- [ ] 资产准入白名单：三方案对比 + 采用理由（GUID 直接依赖 + 路径根传递闭包）+ 清单形状 + 双向真实性 + 生成器的 codeSha256。
- [ ] 成本模型：八项可机器计算的函数 + 六档上限表 + 新增分项结构预算 + `C_over` 的估计方法与诚实边界 + 超限错误码 + 降级的确定性顺序。
- [ ] 控制器 v2：任意阶段集合 / `sustain` 循环与安全阀 / 入事件二分分派 / 出事件双形式与 struct payload / 池化复位 9 项 / 外部引用为空的 6 条退化 / 参数块与绑定表 / 标准与特有参数的三种暴露方式 / 编排内联展开。
- [ ] 绑定 allow-list v2：保留禁反射内核 + 三段键 + 分族表 + **总键数 ~112 且不随内容增长**的论证 + 编译期解析六步（recipe 字符串不进产物）。
- [ ] 门禁谓词：全族合计 156 条，每族 ≥ 3；编译器侧 32 条 + 折射 5 条；fail-closed 三路统一定义；显式豁免机制 5 条（含"三路不可豁免"）。
- [ ] 三件套写入面：结论"不修订" + 三条理由 + 一处 T3 衔接（临时目录前缀登记）。
- [ ] 折射可选层的编译期处理 + 与深度软化的差异表 + 报告声明格式 + 5 条谓词。
- [ ] 新增错误码 14 个。
- [ ] 正文无具体特效名；无序列帧；技术族只有 5 族；无资产外事项进入产物。
