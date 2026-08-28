# Unity 特效资产生产规格 v1（草案）

状态：待用户审阅后作为下一轮验证门禁。  
适用环境：Unity 2022.3 LTS、URP 14、Particle System、Mesh/Trail Renderer；暂不把 VFX Graph 设为默认实现。

## 1. 先澄清截图中的内容

Unity 的 Hierarchy 显示的是场景中的 GameObject 和 Prefab 实例，不是磁盘文件列表。复杂特效为了使用不同的 Renderer、Material、Simulation Space 或生命周期，拥有多个子 GameObject 是正常的。

问题不在于“有子物体”，而在于：

- 空的阶段容器、仅承载一个辅助脚本的对象是否真的必要；
- 旧版本 Mesh、Material、Prefab 是否仍混在活动资源目录；
- 每次生成是否复制已有共享资源；
- Preview 驱动、测试证据和 Authoring 工具是否混入最终运行 Prefab；
- 过期生成物是否能被构建器可靠清理。

Unity 官方将 Prefab 定义为可复用的 GameObject、组件和子对象配置，适合运行时生成特效与 Projectile；Nested Prefab 和 Prefab Variant 用于复用共同结构和保存有意义的变化。Hierarchy 本身则显示场景中的全部 GameObject、相机和 Prefab 实例。

## 2. 本规格的核心原则

1. **一个可投放特效 = 一个运行 Prefab。** 游戏逻辑只引用根 Prefab，不引用 Preview Scene、Recipe 或证据文件。
2. **一个视觉职责 = 一个必要渲染节点。** 只有不同 Renderer、Particle System、独立 Transform 或独立生命周期确实需要时，才创建子 GameObject。
3. **共享资产只存一份。** Shader、通用 Material、Noise、Spark Atlas 和通用 Mesh 放在 Shared Library；每个 Generated 特效不得机械复制。
4. **变化优先参数化。** 颜色、宽度、Reveal、Dissolve、强度等优先使用序列化参数或 MaterialPropertyBlock；只有 Shader Keyword、Render State 或贴图集合确实不同时才创建独立 Material。
5. **源码、成品、预览、缓存、证据分离。** Runtime Player 只接触最终 Prefab 及其依赖。
6. **旧版本不留在活动目录。** 由 Git 历史保存旧实现；需要审计的证据放到仓库外归档或明确的 Archive，不继续被 Unity 导入。
7. **构建必须收敛。** 同一 Recipe 连续构建应得到同样的资产集合；构建器同时删除已不再属于该效果的旧生成物。
8. **运行时采用池化生命周期。** 高频创建的特效从对象池获取，播放结束后归还，不持续 Instantiate/Destroy。

## 3. 标准目录

```text
project/
├─ Assets/VFX/
│  ├─ Effects/                         # 可直接给游戏使用的最终资产
│  │  └─ <Archetype>/<EffectId>/
│  │     ├─ VFX_<EffectId>.prefab     # 必需，唯一运行入口
│  │     ├─ VFX_<EffectId>_Data.asset # 可选，唯一的本地数据/合并 Mesh 资产
│  │     └─ Local/                     # 可选，仅放真正不可共享的依赖
│  ├─ Shared/
│  │  ├─ Shaders/
│  │  ├─ Materials/
│  │  ├─ Textures/
│  │  └─ Meshes/
│  ├─ Recipes/<Archetype>/             # AI/人工 Authoring 输入
│  └─ Preview/                         # 固定验收场景，不进入最终 Prefab
├─ Packages/com.vfxcomposer.unity/
│  ├─ Runtime/                         # 播放、池化、公共控制器
│  ├─ Editor/                          # Compiler、Importer、Preview 工具
│  ├─ Tests/
│  ├─ Samples~/
│  └─ Documentation~/
├─ ProjectSettings/VFXComposer/
│  └─ BuildManifests/                  # 构建记录；不作为 Runtime Asset 导入
└─ Library/VFXComposer/                # 可删除缓存；不进入 Git

docs/vfx-reviews/<EffectId>/            # 人工验收摘要，只保留最终一轮
artifacts/vfx-evidence/<run-id>/        # CI/详细逐帧证据，默认不进入 Unity Assets
```

说明：项目现在的 `Assets/VFX/Generated` 可在迁移期继续存在，但目标语义应改成上面的 `Effects`：它是可投放成品区，不是每次构建无限追加的日志区。

Unity 官方 UPM 布局同样把 Runtime、Editor、Tests、Documentation~ 分开；本项目继续沿用这一边界。

## 4. 单个特效的文件契约

### 4.1 必需文件

| 文件 | 数量 | 是否进入 Player | 规则 |
|---|---:|---:|---|
| `VFX_<EffectId>.prefab` | 1 | 是 | 游戏唯一运行入口 |
| Recipe JSON | 1 | 否 | Authoring 源，不由游戏加载 |
| Build Manifest | 1 | 否 | 放 `ProjectSettings` 或外部 artifacts |

### 4.2 可选文件

| 文件 | 推荐上限 | 允许条件 |
|---|---:|---|
| 本地 `.asset` | 1 | 合并该效果独占的程序化 Mesh/曲线数据；优先使用 sub-assets |
| 本地 `.mat` | 0–2 | Render State、Keyword 或贴图组合无法由共享材质+实例参数表达 |
| 本地纹理 | 0–3 | 该效果独占且不能进入共享 Atlas；必须记录尺寸、格式和压缩策略 |
| 本地 Shader | 0 | Shader 必须进入 Shared Library；禁止每个效果复制一份 |
| 本地脚本 | 0 | 效果能力必须来自 Runtime 包的可复用组件 |

这些数量是本项目的生产预算，不是 Unity 官方硬限制。超过预算可以存在，但必须在 Manifest 中给出理由并通过人工审查。

## 5. Prefab Hierarchy 契约

标准 Slash 示例：

```text
VFX_Slash_Fire                         [VfxEffectController, VfxPoolHandle]
├─ Main                                [MeshFilter, MeshRenderer, Reveal/Fade]
├─ Afterimage                          [MeshFilter, MeshRenderer, Fade]
├─ Sparks                              [ParticleSystem, ParticleSystemRenderer]
├─ Dissipation                         [ParticleSystem, ParticleSystemRenderer]
└─ Ignition                            [可选：ParticleSystem 或 MeshRenderer]
```

规范：

- 普通效果建议 `1` 个根节点、`3–6` 个视觉子节点；总 GameObject 建议不超过 `10`，最大深度不超过 `2`。
- 复杂效果上限建议为 `16` 个 GameObject、深度 `3`；超过时必须说明每个节点不可合并的理由。
- 根节点只承担统一 API、生命周期、池化和全局参数，不承担 Preview 自动播放。
- 禁止仅为了分类而创建 `Anticipation/Primary/Afterimage/...` 空容器；阶段是时间线数据，不应自动变成 Hierarchy 层级。
- 禁止仅承载单一 helper 的 GameObject。`WidthControl`、`SweepRunner`、`Fade` 等组件应放到它所控制的 Renderer 节点或根控制器上。
- 每个 Particle System 可以拥有自己的 GameObject，这是正常结构；不同材质、Simulation Space 或 Renderer 的粒子不要强行合并。
- 所有节点名称必须唯一且表达视觉职责，例如 `Main`、`Sparks`，不使用 `Arc_sweep`、`Primary_arc` 等同义重复名称。
- Preview Camera、Scale Reference、Preview Driver、Evidence Recorder 不得进入最终运行 Prefab。

## 6. 材质、纹理和 Mesh 规则

### 6.1 Material

- 共享 Shader + 共享基准 Material。
- 每实例颜色、Reveal、Dissolve、宽度等使用已声明的实例属性或 MaterialPropertyBlock。
- 不在运行时访问 `renderer.material` 制造隐式材质副本。
- 需要不同透明混合、Cull、ZWrite、Render Queue 或 Shader Keyword 时，才创建独立 Material。
- 共享 Material 必须版本化；破坏性修改创建 `v2`，而不是悄悄改变所有旧效果。

Unity 官方说明，GPU Instancing 的实例属性可以由 MaterialPropertyBlock 提供；非实例属性放入 MaterialPropertyBlock 会破坏 Instancing，因此本项目需要显式区分“实例参数”和“材质变体”。

### 6.2 Texture

- 主体 Mask、Noise、Spark Flipbook 优先进入共享 Atlas 或公共纹理库。
- 纹理文件名不得带开发阶段号（如 `S12`、`S15`）；使用视觉语义和版本号，例如 `T_SlashFire_Main_v1.png`。
- 同一效果不得同时保留“旧 Base”和“新 Main”而只引用其中一个。
- 透明纹理必须检查边缘、MipMap、Wrap、Filter、压缩和最大尺寸。

### 6.3 Mesh

- 一个程序化视觉层不应自动生成多个历史 `.asset`。
- 同一效果的独占 Mesh 合并保存到一个 `VFX_<EffectId>_Data.asset` 中作为 sub-assets，或放入共享 Mesh 库。
- 构建器根据稳定 ID 更新已有 sub-asset，删除已失效 sub-asset，不在文件名中累积阶段号。
- Ring、Aura、Area 边界与其他闭环 Mesh 必须复用首尾边界数据；能够精确共享时，不允许用“接近相等”代替闭合。
- 闭环 Mesh 使用的 UV、Noise、法线、溶解和颜色函数必须周期连续；逻辑分段不等于视觉分段。详细经验与门禁见 `docs/rules/60_ENGINEERING_LESSONS.md` 的 `EXP-003/EXP-004`。

## 7. Recipe、Prefab 和 Variant 的职责

- Recipe 描述意图、时间线、模块选择和可调参数。
- Compiler 把 Recipe 编译为一个最终 Prefab，并引用稳定、版本化的共享依赖。
- 同一结构的小变化使用 Recipe Patch、参数覆盖或 Prefab Variant；不要完整复制一套纹理、Shader 和 Mesh。
- Prefab Variant 只保存有意义的差异，基础结构继续继承公共 Prefab。
- 构建 Hash 必须包含 Recipe、Compiler 版本和递归依赖 Hash。

Unity 官方说明 Nested Prefab 保留到源 Prefab 的连接；Prefab Variant 保存相对于基础 Prefab 的覆盖，适合表达可复用结构的变体。

## 8. Runtime 生命周期

最终 Prefab 必须提供最小统一接口：

```csharp
Play(in VfxPlayContext context)
Stop(VfxStopMode mode)
ResetForPool()
bool IsAlive { get; }
```

规则：

- 高频技能特效由 Pool 复用，不在每次施法时反复 Instantiate/Destroy。
- 所有 Particle System 设置明确的 `maxParticles`。
- 可复现验收时关闭 Auto Random Seed 并记录 seed；正式游戏可按需求选择随机。
- 非循环效果结束后归还对象池；循环 Aura/Area 必须由显式 Stop 结束。
- Preview 自动循环只存在于 Preview Scene Driver。

Unity 官方 Particle System Main 模块提供 `Max Particles`、确定性 Random Seed 和 Stop Action；Unity 官方教学也建议对频繁生成/销毁的对象使用 Object Pool。

## 9. 标准构建事务

```text
Validate Recipe
  → Resolve versioned shared dependencies
  → Build into staging area
  → Validate Prefab hierarchy and dependency closure
  → Atomically replace retained Prefab/data
  → Delete stale outputs that belonged to the previous manifest
  → Write non-runtime Build Manifest
  → Open fixed Preview scene
```

构建门禁：

- 不创建重复 Shader 或未使用 Material。
- 不留下 `_tmp`、backup、pending 或旧阶段资产。
- 保留最终 Prefab GUID。
- 第二次构建文件集合和内容 Hash 一致。
- Prefab 无 Missing Script、Missing Material 或场景对象引用。
- Runtime 程序集不得引用 `UnityEditor`。

## 10. 五层验收

### Gate A：资产结构

- [ ] 一个最终 Prefab。
- [ ] 无 Preview/Editor/Test 组件进入 Prefab。
- [ ] GameObject 数量、深度、Renderer 数量有报告。
- [ ] 没有无职责空节点和 helper-only 节点。

### Gate B：依赖与文件

- [ ] 所有依赖可从 Prefab 递归解析。
- [ ] 无重复 Shader、纹理和不必要材质副本。
- [ ] 无未引用资源。
- [ ] 生成目录只包含当前 Manifest 声明的文件。

### Gate C：Runtime

- [ ] `Play/Stop/ResetForPool/IsAlive` 行为正确。
- [ ] 连续播放不产生材质实例泄漏。
- [ ] 对象池重复使用后状态完全复位。
- [ ] Particle 上限和 seed 策略明确。

### Gate D：视觉

- [ ] 同一 Preview Scene、同一序列化 MainCamera。
- [ ] 真实 Update 播放，不使用手工 Emit/跳时采样伪造画面。
- [ ] 人工审阅短动画和关键帧后才判视觉通过。

### Gate E：性能与交付

- [ ] 记录峰值 Particle、Renderer、Material、Draw Call、CPU/GPU 帧耗。
- [ ] Mobile/PC 预算分别评估。
- [ ] Player Build 通过。
- [ ] 最终包不包含 Recipe、Manifest、Preview、测试和证据。

## 11. 当前 Slash 差距审计（2026-08-23）

### 当前事实

- `Templates/3D/Slash`：`37` 个非 `.meta` 文件，约 `5.95 MB`。
- 其中：`19 Mesh + 6 Material + 5 Prefab + 4 Texture + 3 Shader`。
- `Generated/slash_3d_stylized`：`1 Prefab + 4 Material + 1 BuildManifest`，约 `389 KB`。
- Generated Prefab：`17 GameObject + 4 MeshRenderer + 3 ParticleSystemRenderer + 3 ParticleSystem + 5 MonoBehaviour`。
- `docs`：`686` 个证据/说明文件；`test-results`：`163` 个文件。
- `spike` 本地目录约 `2.35 GB`，主要为一次性 Unity 工程及其缓存。

### 判定

- 用户截图中的 Preview Scene 顶层结构（Camera + Prefab + Driver）合理。
- Generated 文件数不算严重，但四份独立 Material 仍应审查能否共享。
- Prefab 的 `17` 个 GameObject 超出本规格普通特效建议值；真正 Renderer 为 `7` 个，说明至少存在若干可去除容器/helper。
- 最大问题位于 Template/Authoring 活动区：S12 旧 Mesh/Shader/Material 与 S15 新实现共同存在，构建成功后没有执行 obsolete-asset cleanup。
- 研究证据、失败 cohort 和 spike 工程适合归档，不应继续占据日常开发工作区。

### 下一轮建议目标（先不执行删除）

- 最终 Prefab：从 `17` 个 GameObject 收敛到 `8–10` 个，深度不超过 `2`。
- 最终运行依赖：`1 Prefab + 0–1 Data.asset + 0–2 local Material`；其余引用 Shared。
- Slash 活动模板：只保留当前实现使用的 `1–3` 个 Texture、`1–2` 个 Shader、必要共享 Material 和一个合并 Mesh Data。
- 删除动作必须先生成引用报告和迁移清单；经用户确认后，用 Unity AssetDatabase 执行并保留最终 Prefab GUID。
- 旧视觉证据只保留最终通过轮的摘要和动画；详细历史移出活动仓库或打包归档。

## 12. 下一轮验证流程

1. 先对现有 Slash 生成“引用中 / 未引用 / 可合并 / 必须保留”清单。
2. 只做结构瘦身，不修改视觉、相机、时间线和 Recipe 语义。
3. 构建新 Prefab，验证 GUID、视觉帧 Hash 或允许的像素差、播放时间和锚点不变。
4. 用户在同一 Preview Scene 复验。
5. 通过后才归档/删除旧资产。
6. 执行完整 EditMode、PlayMode、Player Build 与池化重复播放测试。

## 13. 官方依据

- Unity Prefab 介绍：https://docs.unity3d.com/2022.3/Documentation/Manual/Prefabs.html
- 创建 Prefab：https://docs.unity3d.com/cn/2022.3/Manual/CreatingPrefabs.html
- Nested Prefab：https://docs.unity3d.com/2022.3/Documentation/Manual/NestedPrefabs.html
- Prefab Variant：https://docs.unity3d.com/2022.3/Documentation/Manual/PrefabVariants.html
- Hierarchy 窗口：https://docs.unity3d.com/Manual/hierarchy-reference.html
- Package layout：https://docs.unity3d.com/Manual/cus-layout.html
- Particle System Main module：https://docs.unity3d.com/cn/2022.3/Manual/PartSysMainModule.html
- GPU Instancing 与实例属性：https://docs.unity3d.com/2022.1/Documentation/Manual/gpu-instancing-shader.html
- Unity Object Pool 教学：https://learn.unity.com/tutorial/65df850fedbc2a082fb11029
- 减少构建文件体积：https://docs.unity3d.com/ja/2022.3/Manual/ReducingFilesize.html
- VFX Graph（Unity 2022.3）：https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.visualeffectgraph.html

## 14. 技术选择说明

本项目暂时继续以 Particle System + Mesh/Trail Renderer + URP Shader 为默认实现。Unity 2022.3 官方文档说明 VFX Graph 在 HDRP 为生产可用，而当时对 URP 和兼容移动设备的完整支持仍在开发中。因此，在 Unity 2022.3/URP14 且未来考虑移动端的前提下，不应为了减少 Hierarchy 节点而盲目切换 VFX Graph。后续可以把它作为高粒子量 PC 特效的可选后端。
