# 50 机器门禁实现

## 1. 权威位置

- 人工规则：`docs/rules/`
- 机器配置：`project/ProjectSettings/VFXComposer/VfxProjectRules.json`
- 统一实现：`project/Packages/com.vfxcomposer.unity/Editor/Rules/`
- 权威产物 Manifest：`project/ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json`
- 自动测试：`project/Packages/com.vfxcomposer.unity/Tests/EditMode/ProductionRulesTests.cs`

## 2. 当前强制项

每次 Projectile v1、Slash v2 或 Impact 2D v1 `Build` 成功提交前/后，统一门禁实际检查：

- Runtime Entry 存在且由对应输出目录拥有；
- 新成品 Prefab 命名为 `VFX_<effectId>`；
- GameObject 数量、最大深度、本地 Material/Texture/Shader 预算；
- Missing Script、Material、Shader；
- Editor、Preview、Evidence 组件不得进入 Runtime Entry；
- 节点名唯一；
- 递归依赖必须位于允许根；
- 本地 owned output 必须从 Runtime Entry 可达，避免未引用旧文件继续堆积；
- `ownedOutputs[]` 记录路径、GUID、类型和文件 SHA-256；
- `dependencies[]` 记录路径、GUID、类型和 dependency hash，不授予删除权；
- 成本同时记录 Local 独占与完整依赖驻留 Texture 内存；
- 严格成品必须能反查到 `Assets/VFX/Recipes` 中保存的 Recipe；
- 规则失败参与原编译事务回滚，外置 Manifest 同样恢复。
- 相同输入的 unchanged Build 保留 `generatedAtUtc`；Manifest 字节未变化时不重写文件。
- Runtime Prefab 根必须且只能有一个 `IVfxRuntimeEntry`，统一提供 Initialize、Play、SendEvent、Stop、ResetForPool 与 IsAlive。

## 3. 严格与遗留

`VfxProjectRules.json` 中只有明确登记的三项正式旧成品和封闭白名单内的隔离历史测试 ID 使用 `legacy_audit`。其结构/推荐上限问题记录为 warning；Missing Script、Editor/Preview 组件、越界依赖等运行安全问题仍是 error。任何未登记的新 EffectId 默认 `strict`，包括全部 `cap_*` 能力素体。测试会对 legacy 白名单做精确集合断言，防止为修复一个历史测试而把产品前缀或通配范围误设为例外。

旧三项必须在 M2/M3/M5 迁移后逐项移出 legacy 清单；不得把新产品 ID 加入 legacy 清单来绕过门禁。测试 ID 只允许测试程序集创建并必须在清理阶段删除。

## 4. 尚未授权的迁移

ADR-001 仍是 Proposed，因此本轮不执行：

- Prefab 深拷贝/嵌套策略变化；
- Generated Material/Texture 自动改为 Shared；
- 对现有活动资产的移动、重命名或删除；
- 基于新 Manifest 的自动 stale 删除。

当前门禁会把严格产物中的不可达 owned output 作为错误阻断。等 ADR-001 Accepted 后，再实现可回滚的 stale 自动删除和 Shared 迁移，不能用静默删除代替决策。

## 5. Visual Module / Atlas 待补机器门禁

`25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md` 已作为人工 MUST/SHOULD 规则生效，但当前机器实现尚未完整检查以下项目，不得误报为自动通过：

- Concepts/ArtSource 不进入 Runtime 递归依赖；
- Source 与 Runtime Atlas 的来源/hash 对应；
- Tight Crop/Padding、Atlas 单元利用率和稳定 ID/Rect/Pivot/方向；
- 同类显著粒子的变体数量或等价随机化；
- Importer Max Size、Read/Write、sRGB/Linear、MipMap 与平台压缩；
- Source 文件、Build 磁盘和 GPU Resident Memory 三栏；
- 可选择的 `exportProfile`、Texture Scale、Max Atlas Size、平台压缩与磁盘/GPU 预算，并将请求值和实际解析值写入 Manifest；
- 最大合理拆解报告：重复次数、模块消费者、完整/拆分/程序化决策、拆解前后 Source/Build/GPU 成本，以及新增 Renderer/Particle/Material/Draw Call；
- Shared 资产消费者数量/获批 Family 复用计划；
- 方向性 Sprite 尖端与运动方向一致；
- 通用 Ring/Tile/循环 Mesh 的首尾顶点、法线和 UV 闭合；当前只有 `frost_impact_2d` 的定向回归，不能代表全 Archetype 已覆盖；
- Shader/纹理周期采样声明及 seam 两侧连续性检查；
- Compiler 实现内容 Hash 自动进入 Build Hash，避免行为已改变却被 Dry Run 判为 `Unchanged`；
- Material 更换 Shader 后清理旧纹理槽/关键字，并递归证明废弃 GUID 不可达；
- 权威 `16:9` 之外的 `4:3`、`1:1`、超宽视口裁切与可读性检查；
- 资源优化前后关键帧的感知差异、模块屏幕占比与已知失败特征报告。

上述检查应在 M5 统一 Schema/Manifest 后进入 `VfxProductionRules`；在此之前由 Gate 0/1/3/5/6 清单和人工报告执行，不能以“尚无机器错误”代替合规。
