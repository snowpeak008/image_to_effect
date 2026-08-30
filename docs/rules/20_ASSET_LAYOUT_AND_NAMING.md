# 20 目录、文件与命名

## 1. 目标目录

```text
project/
├─ Assets/VFX/
│  ├─ Effects/
│  │  └─ <Archetype>/<EffectId>/
│  │     ├─ VFX_<EffectId>.prefab
│  │     ├─ VFXD_<EffectId>.asset          # 可选，独占数据/sub-assets
│  │     └─ Local/                         # 可选，真正不可共享依赖
│  ├─ Shared/
│  │  ├─ Shaders/
│  │  ├─ Materials/
│  │  ├─ Textures/
│  │  ├─ Meshes/
│  │  └─ Atlases/
│  ├─ Templates/<2D|2.5D|3D|Screen>/<Archetype>/
│  ├─ Recipes/<Archetype>/
│  └─ Preview/
├─ Packages/com.vfxcomposer.unity/
│  ├─ Runtime/
│  ├─ Editor/
│  ├─ Tests/EditMode/
│  ├─ Tests/PlayMode/
│  ├─ Samples~/
│  └─ Documentation~/
├─ ProjectSettings/VFXComposer/BuildManifests/
└─ Library/VFXComposer/                  # ignored，可删除

docs/vfx-reviews/<EffectId>/             # 最终人工审阅摘要
artifacts/vfx-evidence/<run-id>/          # CI/详细证据，不进入 Assets

ArtSource/VFX/<Family>/                   # 概念图、分层源文件、Atlas 排布源；Unity 不导入
```

## 2. 每个成品包允许的文件

| 类型 | 必需/上限 | 说明 |
|---|---:|---|
| Runtime Entry | 必需 `1` | 默认是 Runtime Prefab；Environment 系统级例外须有 waiver |
| Runtime Prefab | 默认 `1` | 技能/角色/UI 特效的游戏入口；使用获批非 Prefab Entry 时可为 `0` |
| Data Asset | `0–1` | 合并独占 Mesh/曲线/sub-assets |
| Local Material | 推荐 `0–2` | 仅不可由共享材质表达时 |
| Local Texture | 推荐 `0–3` | 独占内容；必须有导入和来源记录 |
| Local Shader | `0` | 全部进入 Shared |
| Local Runtime Script | `0` | 能力进入 Package Runtime |
| Recipe | 必需 `1`，不进 Player | Authoring 输入 |
| Build Manifest | 必需 `1`，不进 Player | ProjectSettings/artifacts |

超过推荐上限需要 waiver；不是 Unity 硬限制。

存放位置说明：Recipe 放在 `Assets/VFX/Recipes/` 是因为 Compiler 通过 AssetDatabase 读取它并参与依赖追踪；Build Manifest 放在 `ProjectSettings/VFXComposer/` 是因为它是构建事务记录，不应被任何资产引用。两者都 MUST 通过构建剥离验证（Gate 8）确认不进入 Player。

## 3. 命名

### 3.1 ID

- Effect/Phase/Module ID：`lower_snake_case`，稳定且不包含显示名。
- ID 不包含开发阶段号、日期、Agent 名或临时状态。
- Revision 是数据字段，不嵌入 ID。

### 3.2 Unity Assets

| Asset | 规则 | 示例 |
|---|---|---|
| Runtime Prefab | `VFX_<EffectId>` | `VFX_slash_fire.prefab` |
| Template Prefab | `PFT_<Dim>_<Archetype>_<Role>` | `PFT_3D_Slash_Main.prefab` |
| Data | `VFXD_<EffectId>` | `VFXD_slash_fire.asset` |
| Shared Material | `MAT_<Family>_<Variant>` | `MAT_SlashPainted_Additive.mat` |
| Local Material | `MAT_<EffectId>_<Role>` | `MAT_slash_fire_Core.mat` |
| Shader | `SHD_<Pipeline>_<Family>` | `SHD_URP_PaintedCrescent.shader` |
| Shared Texture | `T_<Family>_<Role>_vN` | `T_SlashFire_Main_v1.png` |
| Local Texture | `T_<EffectId>_<Role>_vN` | `T_slash_fire_Main_v1.png` |
| Mesh | `MESH_<Family>_<Role>` | `MESH_Slash_ActionPlane` |
| Preview Scene | `VFXPREVIEW_<Archetype>` | `VFXPREVIEW_Slash.unity` |

Local 资产以 `<EffectId>` 命名、Shared 资产以 `<Family>` 命名——审计时凭名称即可判断所有权；名称含 EffectId 却位于 Shared、或反之，均视为 Gate 3 结构违规。

禁止生产命名：`S12_*`、`S15_*`、`final_final`、`new`、`copy`、Agent 名、时间戳。

## 4. Material 与 Shader

- 一个 Shader Family 对应一种渲染语义，不对应单个 EffectId。
- Render State/Keyword 不同可拆 Material；仅数值不同使用实例参数。
- MaterialPropertyBlock 只能写 Shader 已声明为实例化或明确支持的属性；不得把任意非实例属性塞入 MPB 后声称保持 Instancing。
- 透明材质必须明确 Blend、ZWrite、Cull、Queue、Sorting 和软粒子策略。
- 不得在 Build 时为每个 Renderer 无条件复制 Material。

## 5. Texture

- 所有位图模块的生产、裁剪、Atlas、变体和复用 MUST 遵守 `25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md`。
- 必须记录：用途、来源、许可/生成信息、SHA-256、Alpha、尺寸、色彩空间、Wrap、Filter、MipMap、压缩和最大尺寸。
- Mask/Noise 默认 Linear；颜色纹理按实际用途决定 sRGB。
- UI/Pixel Art、2D、3D 的导入预设分开。
- 已被替换且无引用的旧纹理不得继续留在活动 Templates。
- Atlas 中每个区域必须有稳定语义索引；禁止代码依赖偶然排列。
- 完整概念图、AI 原图和 PSD 属于 ArtSource，未经 Tight Crop、定尺、Alpha 清理和平台压缩不得成为 Runtime 依赖。
- 纹理尺寸由目标屏幕投影决定，透明空白、Atlas 利用率和完整依赖驻留内存必须报告；Shared 名称不豁免。

## 6. Mesh 与曲线数据

- 通用 Quad、Ring、Arc、Spawn Shape 放 Shared。
- 独占程序化 Mesh 集合合并进一个 Data Asset，并以稳定 sub-asset 名更新。
- 禁止每轮构建增加新的 Mesh 文件而不删除旧文件。
- Mesh 必须记录顶点/三角形、Bounds、UV、Pivot 和是否可读。
- Anchor/Pivot 是资产契约；层间共享 Anchor 必须有自动测试。

## 7. Prefab 与 Variant

- 共用结构的风格变化优先 Recipe/Patch 或 Prefab Variant。
- Nested Prefab 用于组合已批准的可复用子效果。
- Variant 只保存有意义差异，不 Apply 回基础 Prefab 破坏其他效果。
- Managed Prefab 不允许手工 Unpack 后作为正式成品。
- 所有 GameObject 名称必须唯一，避免覆盖保存时引用匹配不确定。

## 8. `.meta`、GUID 与引用

- Assets 下每个文件和文件夹的 `.meta` MUST 提交。
- 构建更新保留正式 Runtime Prefab GUID。
- 移动/重命名必须通过 Unity AssetDatabase，不能直接丢失 `.meta`。
- 删除前必须生成反向引用清单。
- 构建输出不得引用 Preview Scene、Editor-only Asset、绝对磁盘路径或临时目录。

## 9. Generated/Effects 收敛规则

每次 Build Manifest 必须列出该 EffectId 拥有的完整输出集合。成功提交时：

- 新集合原子替换旧集合；
- 旧 Manifest 拥有但新 Manifest 不再包含的文件被标为 stale；
- stale 文件只有在无外部引用且事务可回滚时删除；
- 不属于当前 EffectId 的用户文件绝不删除；
- 失败恢复旧 Prefab、GUID、文件集合和 Manifest；
- 连续两次相同 Build 的文件名和 SHA-256 相同。

### 9.1 Build Manifest 目标最小字段

在 M5 提供正式 JSON Schema 前，所有新 Compiler 和迁移后的旧 Compiler 至少 MUST 写出以下语义字段：

```text
manifestVersion
effectId
recipeVersion
recipeRevision
recipeHash
buildHash
compilerVersion
unityVersion
sourceRecipePath
runtimeEntry { kind, path, guid }
ownedOutputs[] { path, guid, assetType, sha256 }
dependencies[] { path, guid, assetType, version }
cost { particles, particleSystems, renderers, materials, trails, duration,
       localTextureBytes, dependencyResidentTextureBytes }
generatedAtUtc
```

字段规则：

- `runtimeEntry.kind` 至少允许 `prefab`；Environment waiver 后 MAY 使用 `scene_service` 或 `runtime_asset`。
- `ownedOutputs[]` 是 stale 清理的唯一所有权依据，MUST 完整列出该 EffectId 生成且允许构建器替换/删除的资产；每项包含稳定路径、GUID、Unity 资产类型和保存完成后的 SHA-256。
- `dependencies[]` 只记录引用关系，MUST NOT 赋予当前 Effect 对共享资产的删除权；版本不可用时显式为 `null`，但 GUID 和 dependency hash 仍必需。
- Unity 官方 Package 依赖可进入 `dependencies[]`，但必须由项目规则按精确包根白名单声明，例如 Screen/UI Runtime Entry 所需的 `Packages/com.unity.ugui/`；不得用宽泛 `Packages/` 放行未知第三方依赖。
- `.meta` 随其对应 owned asset 受同一事务管理，不作为独立 owned output；删除或移动必须通过 AssetDatabase 并保持引用安全。
- Build Manifest 自身位于 `ProjectSettings/VFXComposer/BuildManifests/`，不列入 `ownedOutputs[]`，但必须与资产提交处于同一可回滚事务。
- `generatedAtUtc` 不参与 `buildHash`；相同输入的 unchanged Build 不得仅因时间戳重写 Manifest。
- 当前 v1 和 Slash v2 Manifest 尚未满足本字段集；这是迁移目标，不得把现状误报为已通过。
- M5 MUST 为该字段集建立正式机器 Schema、版本迁移和 v1/v2 一致性测试。

## 10. 非产品文件

以下内容不得进入 Player 资产依赖，也不应长期堆在活动 Unity Assets：

- Preview screenshots、GIF、逐帧 PNG；
- rejected runs、AI cohort 原始对话、临时 prompt；
- test-results、coverage、Unity logs；
- spike 工程的 Library/Temp/Logs/UserSettings；
- backup、pending、staging、transaction snapshot。

日常仓库只保留最终验收摘要和必要追溯记录；完整研究证据应打包到外部 artifacts/archive。

## 11. 官方依据

以下链接统一使用 Unity 2022.3（项目目标版本）中文文档：

- Prefab 概述：https://docs.unity3d.com/cn/2022.3/Manual/Prefabs.html
- 创建 Prefab 与唯一名称注意事项：https://docs.unity3d.com/cn/2022.3/Manual/CreatingPrefabs.html
- Nested Prefab：https://docs.unity3d.com/cn/2022.3/Manual/NestedPrefabs.html
- Prefab Variant：https://docs.unity3d.com/cn/2022.3/Manual/PrefabVariants.html
- Unity Package Layout：https://docs.unity3d.com/cn/2022.3/Manual/cus-layout.html
- GPU Instancing 属性：https://docs.unity3d.com/cn/2022.3/Manual/gpu-instancing-shader.html
