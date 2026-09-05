# ADR-009：模板视觉质量标准（按 kind 的最低可断言谓词闭集）

状态：`ACCEPTED`（2026-09-05，T1b 交付双组审计 PASS 后由主 agent 签署；2D 4 模板重制达标、豁免表退役 2D 行为其落地证据）
日期：2026-09-03（PROPOSED）/ 2026-09-05（ACCEPTED）
规范令牌：`TEMPLATE_VISUAL_QUALITY_V1`

本 ADR 立法的是唯一一个新问题：**一个 prefab 要以某个 `kind` 进入模板库（`Assets/VFX/Templates/**`），它必须满足哪些机器可断言的最低视觉行为**。它不裁美学品味（见 §7），不改变 manifest 契约（`manifestVersion: 1` 字段不动），不触碰 ADR-001/007 的资产写入边界。

## 1. 背景

九宫格验收（2026-09-03）中用户裁定：`spark_projectile_2d` 及全部 preset 变体的视觉表现不是特效——投射物主体是**一张静态贴图在做位移**。根因追到模板库：

- `PFT_2D_FireCore`（kind=`energy_body`）是**裸 SpriteRenderer + 静态贴图**，无任何 ParticleSystem（prefab 仅 2.3KB），manifest 自己声明 `estimatedPeakParticles: 0`。一个"能量体"模板没有任何随时间变化的视觉行为。
- 模板入库时只有**结构性标准**（manifest 可解析、GUID/路径一致、参数三点可玩、依赖不出 Templates 目录——见 `FormalTemplateIntegrationTests`），**没有视觉质量标准**。"组件齐全但视觉为零"的模板可以合法入库并被 Recipe 编译进成品。

存量核查（2026-09-03，逐 prefab YAML 核实）进一步暴露：任务卡之前记录的"Embers/FireImpact/Shockwave/LaunchFlash 各含 1 ParticleSystem + 1 SpriteRenderer + 1 TrailRenderer"**与事实不符**——四者各只有 1 个 GameObject（ParticleSystem + ParticleSystemRenderer），无任何附加渲染层。视觉质量必须机器断言，不能靠记忆或文档转述。

## 2. 决策：按 kind 的最低视觉谓词闭集

每条谓词都是**对 prefab 资产（组件序列化状态）或 manifest 字段的确定性断言**，EditMode 下用 `AssetDatabase.LoadAssetAtPath<GameObject>` + 组件 API 即可判定，不需要进 PlayMode、不需要渲染采样。谓词编号稳定，测试失败信息必须引用编号。

kind 闭集与 `VfxDomainParser.ModuleKindValues` 对齐（8 值）。本 ADR 对其中 **6 个 kind 立法**；`sprite_emitter` 与 `sub_effect` 当前模板库未使用，**未立法即不得入库**（见 §5 fail-closed）。

### 2.1 `energy_body`（能量体：火核、光球等持续性主体）

| 编号 | 谓词（组件 API 语义） | 序列化对应（prefab YAML） |
|---|---|---|
| EB-1 | prefab 内 ParticleSystem 数 ≥ 1 | 存在 `ParticleSystem:` 文档块 |
| EB-2 | 至少一个 ParticleSystem 同时满足：`emission.enabled` 且（`colorOverLifetime.enabled` 或 `textureSheetAnimation.enabled`）且 `sizeOverLifetime.enabled` | `EmissionModule.enabled: 1`；`ColorModule.enabled: 1` 或 `UVModule.enabled: 1`；`SizeModule.enabled: 1` |
| EB-3 | **禁止以裸 SpriteRenderer 为唯一渲染体**：全部 Renderer 不得都是 SpriteRenderer | 不允许唯一渲染器文档块是 `SpriteRenderer:` |
| EB-4 | manifest `cost.estimatedPeakParticles ≥ 8` | manifest JSON 字段 |

语义：能量体必须有随生命周期变化的颜色/贴图与尺寸行为——静态贴图（哪怕挂在粒子上）加位移不构成能量体。

### 2.2 `impact_burst` / `impact_flash`（命中爆发 / 发射与命中闪光）

| 编号 | 谓词 | 序列化对应 |
|---|---|---|
| IM-1 | 至少一个 ParticleSystem：`emission.enabled` 且 burst 非空（`emission.burstCount ≥ 1`） | `EmissionModule.enabled: 1` 且 `m_BurstCount ≥ 1`（`m_Bursts` 列表非空） |
| IM-2 | 渲染层 ≥ 2：prefab 内 Renderer 组件（含 ParticleSystemRenderer/TrailRenderer/SpriteRenderer 等）总数 ≥ 2 | ≥ 2 个 Renderer 类文档块 |

语义：冲击必须是瞬时爆发（burst）而非匀速流；且必须有主粒子层之外的次级/闪光层，单一粒子层的"命中"没有冲击结构。

### 2.3 `shockwave`（冲击波环）

| 编号 | 谓词 | 序列化对应 |
|---|---|---|
| SW-1 | 至少一个 ParticleSystem：`sizeOverLifetime.enabled` 且尺寸曲线扩张（曲线末键值 > 首键值） | `SizeModule.enabled: 1`，`maxCurve.m_Curve` 末键 `value` > 首键 `value` |
| SW-2 | 同一 prefab：`colorOverLifetime.enabled` 且 alpha 衰减（渐变末 alpha 键 < 首 alpha 键） | `ColorModule.enabled: 1`，`maxGradient` 的 `atime`/alpha 键序末值小于首值 |

语义：冲击波 = 扩张 + 消散，两者缺一即退化为静态环贴图。

### 2.4 `motion_trail`（运动拖尾）

| 编号 | 谓词 | 序列化对应 |
|---|---|---|
| MT-1 | 存在 TrailRenderer 且宽度曲线非常数（`widthCurve` 键数 ≥ 2 且存在两键值不同）；**或**至少一个 ParticleSystem `trails.enabled` | `TrailRenderer.m_Parameters.widthCurve.m_Curve` 多键异值；或 `TrailModule.enabled: 1` |
| MT-2 | 对应的颜色渐变非单色：颜色键存在色差或 alpha 键存在差值 | `colorGradient` 的 `key0/key1…` 色值不全等或 alpha 键不全等 |

语义：等宽单色的拖尾是一条色带，不是运动痕迹。

### 2.5 `secondary_particles`（次级粒子：余烬、碎屑）

| 编号 | 谓词 | 序列化对应 |
|---|---|---|
| SP-1 | 至少一个 ParticleSystem：重力非零（`main.gravityModifierMultiplier ≠ 0`）**或**速度衰减启用（`limitVelocityOverLifetime.enabled`） | `InitialModule.gravityModifier.scalar ≠ 0`（或曲线非零）；或 `ClampVelocityModule.enabled: 1` |

语义：次级粒子必须表现出受物理影响的运动（下坠或减速），匀速直线飞行的点不是余烬/碎屑。

### 2.6 通用（全 kind）

| 编号 | 谓词 |
|---|---|
| G-1 | manifest `assetGuid` 解析路径 = `assetPath`，prefab 可加载（已由 `FormalTemplateIntegrationTests` 覆盖，此处不重复断言，仅作为谓词前置条件：加载失败即失败） |

## 3. 机器强制

EditMode 测试 `project/Packages/com.vfxcomposer.unity/Tests/EditMode/TemplateVisualQualityTests.cs` **构造性遍历**：

1. 枚举 `Assets/VFX/Templates/` 下每个维度目录（`2D`/`3D`/…）；含 `Prefabs/` 的维度目录**必须**含非空 `Manifests/`（否则失败，见 §5）。
2. 每个 `Manifests/*.manifest.json` 经 `TemplateCatalog.LoadFromDirectory` 解析（复用生产解析器，报告有错即失败）。
3. 每个维度目录 `Prefabs/` 下的每个 `.prefab` 必须被某 manifest 的 `assetPath` 引用——**没有 manifest 的 prefab 不得存在于模板库**（绕过质检的旁路封死）。
4. **Slash v2 独立闭域排除**：`3D/Slash/**` 与 `3D/SlashManifests/**`（`.slash.manifest.json` 后缀）是刻意独立的 v2 域，生产 `TemplateCatalog.LoadFromDirectory` 本就拒绝把它重释为 v1 模板；本 ADR 沿用该边界，以测试内**显式闭集常量**（`SeparatelyManifestedSubtrees`）声明并整体跳过——与 R-5"自持清单型容器"纪律同构。闭集外的任何子目录不得以此为由逃逸。
4. 按 manifest `kind` 施加 §2 谓词；prefab 解析用 `AssetDatabase.LoadAssetAtPath<GameObject>` + 组件 API（不做 YAML 文本匹配，序列化格式升级不脆断）。
5. 豁免机制见 §4；豁免表外的模板任一谓词失败 → 测试失败，模板不得入库/交付。

新模板入库的定义性事件是"manifest 出现在 `Manifests/`"——测试遍历是构造性的，新模板自动进入断言面，无需登记。

## 4. 存量处置：显式豁免清单

存量模板于 2026-09-03 按 §2 谓词逐项核查（prefab YAML 实测，非转述）。核查同时暴露：**3D 模板库与 2D 同病**——`PFT_3D_FireCore` 是 MeshRenderer 静态体（无 ParticleSystem、peak=0），四个 3D 粒子模板 ColorModule 全关。

2D（6 模板）：

| 模板 | kind | 谓词判定 | 处置 |
|---|---|---|---|
| `PFT_2D_FireCore` | `energy_body` | EB-1✗（0 个 ParticleSystem）EB-2✗ EB-3✗（唯一渲染体是 SpriteRenderer）EB-4✗（peak=0） | **豁免 → T1b 重制** |
| `PFT_2D_Embers` | `secondary_particles` | SP-1✗（gravityModifier=0 且 ClampVelocityModule 关） | **豁免 → T1b 重制** |
| `PFT_2D_FireImpact` | `impact_burst` | IM-1✓（burst 24/30）IM-2✗（仅 1 个 Renderer） | **豁免 → T1b 重制** |
| `PFT_2D_LaunchFlash` | `impact_flash` | IM-1✓（burstCount=1）IM-2✗（仅 1 个 Renderer） | **豁免 → T1b 重制** |
| `PFT_2D_Shockwave` | `shockwave` | SW-1✓（size 曲线 0.35→2.8）SW-2✓（alpha 1→0） | 达标，正常断言 |
| `PFT_2D_FireTrail` | `motion_trail` | MT-1✓（widthCurve 1→0.46→0）MT-2✓（双色渐变 + alpha 1→0） | 达标，正常断言 |

3D（6 模板；Slash v2 域除外，见 §3-4）：

| 模板 | kind | 谓词判定 | 处置 |
|---|---|---|---|
| `PFT_3D_FireCore` | `energy_body` | EB-1✗（0 个 ParticleSystem，MeshRenderer 静态体）EB-2✗ EB-4✗（peak=0）；EB-3✓（非 SpriteRenderer） | **豁免 → 3D 重制卡（T1b 交付报告中排期）** |
| `PFT_3D_Embers` | `secondary_particles` | SP-1✗（gravity=0 且 ClampVelocityModule 关） | **豁免 → 同上** |
| `PFT_3D_FireImpact` | `impact_burst` | IM-1✓ IM-2✗（仅 1 个 Renderer） | **豁免 → 同上** |
| `PFT_3D_LaunchFlash` | `impact_flash` | IM-1✓ IM-2✗（仅 1 个 Renderer） | **豁免 → 同上** |
| `PFT_3D_Shockwave` | `shockwave` | SW-2✗（ColorModule 关） | **豁免 → 同上** |
| `PFT_3D_FireTrail` | `motion_trail` | MT-1✓（widthCurve 1→0.36→0）MT-2✓（双色渐变） | 达标，正常断言 |

豁免规则：

1. 豁免表是测试内**显式常量表**：`模板 id → (豁免理由, 到期卡号)`。立法时 9 项：2D 侧 4 项到期卡号 **T1b**；3D 侧 5 项到期卡号 **T1b-3D**。**勘注（2026-09-05，T1b 交付）**：2D 4 项已重制达标并从豁免表退役，现存 5 项（全部 3D）；上方 §4 判定表保留重制前实测作历史记录。T1b-3D 排期：与 2D 同病（FireCore 静态 MeshRenderer、四粒子模板单层/ColorModule 关），工作量与 2D 相当（prefab YAML 手写多层），建议 T1 用户视觉复验通过后作为独立卡派发，复用 2D 的层次结构模式与谓词自查清单。
2. 豁免模板**仍然跑谓词**：若某豁免模板实测已全部达标，测试失败并要求把它从豁免表移除（防豁免表陈旧）；若仍不达标，计入"已消费豁免"。
3. 测试最终断言**已消费豁免集合恰等于声明清单**——豁免不可静默扩、不可静默缩（与 O4 R-4 显式豁免、R-5 闭集纪律同构，见 `docs/plans/UNITY_TEST_TRIAGE.md` §6.4/§6.6）。
4. 豁免表内模板的 id 必须真实存在于模板库（幽灵豁免即失败）。
5. **T1b 交付定义**：重制 2D 侧 4 模板至谓词达标，并把它们从豁免表移除；3D 侧 5 项由 T1b 报告排期后续卡。豁免表清空后本节归档为历史记录。

## 5. fail-closed

与 R-5 容器闭集纪律（`docs/plans/UNITY_TEST_TRIAGE.md` §6.6）一致，一切未声明者拒绝：

1. **未知/未立法 kind 拒绝**：manifest `kind` 若不在 §2 已立法 6 值内（含 parser 认识但本 ADR 未立法的 `sprite_emitter`/`sub_effect`），测试失败。要用新 kind，先修订本 ADR 补谓词（MUST 级变更，升规则集版本）。
2. **无 manifest 拒绝**：含 `Prefabs/` 的模板维度目录缺失或空置 `Manifests/` → 失败；`Prefabs/` 下存在未被任何 manifest 引用的 `.prefab` → 失败。
3. **解析失败拒绝**：`TemplateCatalog` 报告任何 error → 失败，不降级为跳过。

## 6. 与既有规则的关系

- `FormalTemplateIntegrationTests` 的结构性断言（GUID/路径/参数三点/依赖边界/纹理导入）**保持独立**，本 ADR 只新增视觉行为层，不替代不重叠。
- `30_ACCEPTANCE_AND_DELIVERY.md` 的成品验收面不变；本 ADR 管的是**模板入库门**，位于成品编译之前。
- manifest 契约（`VfxDomainParser`）零改动；`estimatedPeakParticles` 从"成本申报"升格为 energy_body 的质量下限输入（EB-4），字段语义不变。

## 7. 不作声明

- **不裁美学品味**：谓词只断言"存在随时间变化的视觉行为"这一最低结构事实。颜色搭配、曲线手感、贴图美感属于人工验收（九宫格评审面），机器不裁。
- 不断言运行时粒子数、帧率、模拟结果（那是 PlayMode/性能面，见 `30_ACCEPTANCE_AND_DELIVERY.md`）。
- 不追溯已编译成品（`Assets/VFX/Generated/**`）；成品质量经由"模板达标 + Recipe 编译"间接保证，存量成品的重建由 T1b 及后续卡处理。
- 不为 3D 模板预设额外谓词；3D 模板入库时若 6 kind 谓词不适配，修订本 ADR。
