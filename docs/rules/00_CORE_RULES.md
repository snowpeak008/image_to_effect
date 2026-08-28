# 00 全局核心规则

## 1. 目标

本规则定义任何正式特效必须具备的统一边界。视觉类型可以不同，但成品入口、生命周期、资源所有权、构建事务和验收方式必须一致。

任何新 Effect 在 Gate 0 开始前 MUST 查阅 `60_ENGINEERING_LESSONS.md`，筛选与其 Archetype、Topology、渲染后端和资源策略相关的 `EXP-*`；适用经验必须进入 Brief、测试或人工验收清单，不能只作为历史阅读材料。

## 2. 统一术语

| 术语 | 含义 |
|---|---|
| Effect | 可被游戏调用的一项完整视觉表现 |
| Archetype | Projectile、Slash、Aura 等行为类型 |
| Phase | 有起止时间或事件边界的表现阶段 |
| Module | 单一视觉职责，如主体、火星、冲击环 |
| Template | 可复用的模块 Prefab 与 Manifest |
| Recipe | 与引擎属性解耦的 Authoring 输入 |
| Runtime Entry | 游戏用于加载/创建完整 Effect 的唯一入口；默认是 Runtime Prefab |
| Runtime Prefab | 游戏唯一可实例化入口 |
| Shared Asset | 多个效果共同引用的 Shader、Material、Texture、Mesh |
| Local Asset | 只属于一个 EffectId 的必要依赖 |
| Managed Output | 由 Compiler 拥有、重建会覆盖的成品 |
| Baked Output | 未来 Detach/Bake 后允许人工维护的自包含成品 |

## 3. 通用描述模型

每个 Effect MUST 明确：

```text
identity       id / revision / displayName / tags
archetype      行为类型
dimension      2d / 2.5d / 3d / screen
lifecycle      instant / one_shot / sustained / looping / event_driven
topology       point / directional / moving / segment / area / volume / screen
attachment     world / caster / target / bone / projectile / camera
targetProfile  mobile_medium / pc_editor / 后续真机 Profile
timeline       phases 或 events；不得假设固定 Launch/Travel/Impact
modules        稳定 moduleId、kind、templateId、parameters、attachment
metadata       compiler/catalog 版本与来源信息
```

规则：

- MUST 使用稳定语义 ID，Patch 不得依赖数组下标。
- MUST 拒绝未知字段、未知 Template、越界参数和不合法挂载。
- MUST 显式声明循环与停止条件。
- MUST 将游戏伤害、碰撞判定和命中逻辑留给 Gameplay；VFX Recipe 只描述表现与接收事件。
- 现有 Recipe v1/v2 在迁移前继续有效；本节是统一目标模型，不授权直接修改版本号或破坏旧 Recipe。

当前权威来源按 Recipe 版本和 Archetype 拆分：

- **Projectile Recipe v1**：运行时权威是 `project/Packages/com.vfxcomposer.unity/Editor/Domain/VfxDomainParser.cs` 与 `project/Packages/com.vfxcomposer.unity/Editor/Validation/RecipeValidator.cs`；`docs/ai-workflow/recipe-v1.schema.json` 是 AI 可读的结构说明，不是运行时 JSON Schema 验证器。参数和模板词汇见 `docs/ai-workflow/template-parameters.generated.md`。这与 `docs/DECISIONS.md` §21 / “JSON Schema” 决议一致：项目不引入通用 Schema 验证库，结构和语义由 C# 手写验证。
- **Slash Recipe v2**：运行时权威是 `project/Packages/com.vfxcomposer.unity/Editor/SlashV2/S12SlashV2Domain.cs` 中的 `S12RecipeDispatcher`、`S12SlashV2Parser`、`S12SlashV2Validator`，以及对应 Compiler/Catalog；AI 可读权威快照是 `docs/ai-workflow/s12-slash-v3/frozen/contract.generated.json` 与同目录 `manifest.generated.json`。
- **Impact Recipe v1（2D 最小纵切）**：运行时权威是 `project/Packages/com.vfxcomposer.unity/Editor/Impact2D/Impact2DDomain.cs` 与 `Impact2DCompiler.cs`；只接受 `archetype=impact + dimension=2d + lifecycle=one_shot` 的严格字段集，不得交给 Projectile v1 Parser。它是独立类型纵切，不等同于 M5 的跨 Archetype 统一 Schema。
- 本节的通用描述模型是跨 Archetype Schema 的演进目标，不是当前可直接提交给任一旧 Parser 的 JSON 格式。
- 跨 Archetype 统一 Schema、版本迁移和机器 Schema 文件列入迁移 M5；在 M5 完成前，Dispatcher MUST 先按 `recipeVersion + archetype` 选择对应权威体系，禁止用 Projectile v1 Schema 验证 Slash v2。
- Build Manifest 目前没有统一 Schema；目标最小字段定义见 `20_ASSET_LAYOUT_AND_NAMING.md` §9.1，正式机器 Schema 化列入 M5。

## 4. 一个 Effect，一个 Runtime Entry

- 每项正式特效 MUST 只有一个 Runtime Entry。
- Runtime Entry 默认是一个根 Runtime Prefab；Environment/Weather 等系统级类型 MAY 经 waiver 使用 Scene Service 或专用 Runtime Asset。
- 游戏代码 MUST 只引用 Runtime Entry 或其稳定运行时地址。
- Preview Scene、Recipe、Build Manifest、测试和证据 MUST NOT 成为 Player 运行依赖。
- Runtime Entry 或其适配器 MUST 提供统一能力：初始化上下文、播放、停止、池化复位、查询存活状态。
- Archetype 特有事件通过统一事件接口或专用适配器表达，不在游戏侧遍历子节点。

建议接口语义：

```text
Initialize(context)
Play()
SendEvent(eventId, payload)
Stop(immediate | allow_tail)
ResetForPool()
IsAlive
```

## 5. Hierarchy 规则

- 一个视觉职责 MAY 使用一个 GameObject。
- 不同 Particle System、Renderer、Simulation Space、独立 Transform 或独立生命周期可以分别建节点。
- 阶段分类本身 MUST NOT 自动产生空容器。
- 只承载一个辅助控制脚本的 GameObject SHOULD NOT 存在；脚本应挂到根或被控制 Renderer 节点。
- Preview Driver、Camera、Scale Reference、Evidence Recorder MUST NOT 进入 Runtime Prefab。
- 节点名称 MUST 唯一并表达职责，不同时出现 `Primary_arc`、`Arc_sweep` 这类重复语义。

项目结构预算：

| 等级 | GameObject | 最大深度 | 适用 Archetype |
|---|---:|---:|---|
| Simple | `<= 10` | `<= 2` | Impact、Slash、Trail、Aura、Beam、Shield、Screen/UI、简单 Projectile（默认等级） |
| Complex | `<= 16` | `<= 3` | Area、复合 Projectile、Spawn/Summon/Transform、Environment、Composite |
| Exception | `> 16` | `> 3` | MUST 提供逐节点不可合并说明和性能证据 |

- 本表是等级上限；`10_ARCHETYPE_PROFILES.md` 中每类的建议节点区间是更具体的默认值，两者冲突时以本表上限为硬门禁、以类型建议区间为审阅参考。
- 以上是本项目预算，不是 Unity 引擎硬限制。

## 6. Module 规则

每个 Module MUST：

- 只有一个主要视觉职责；
- 有稳定 `moduleId`；
- 声明 Renderer/Particle/Trail/Material 成本；
- 声明 2D/3D/Screen 兼容性；
- 声明生命周期和 attachment；
- 暴露有限、带类型和范围的参数；
- 能在对象池复用时完全 Reset；
- 不通过反射或任意 Unity 属性路径接受 AI 输入。

Module MAY 组合 Sprite、Mesh、Particle、Trail，但不能以“通用粒子”代替明确的视觉语义。

## 7. 共享与本地资产

- Shader MUST 放入 Shared Library，不得按 EffectId 复制。
- 通用 Noise、Gradient、Spark Atlas、基础 Mesh SHOULD 共享。
- 每实例颜色、Reveal、Dissolve、宽度等 SHOULD 使用序列化实例参数或正确声明的 MaterialPropertyBlock 属性。
- 不得在运行时调用 `renderer.material` 产生隐式材质实例。
- 不同 Render State、Keyword 或贴图组合确实需要时 MAY 使用本地 Material。
- 共享资产 MUST 版本化；破坏性修改创建新版本，不静默改变已批准成品。
- 程序化独占 Mesh SHOULD 合并到一个 Data Asset 的 sub-assets，不为每次迭代保留一批历史 `.asset`。

## 8. 生命周期与对象池

- 高频技能特效 MUST 使用对象池。
- 一次性效果结束后 MUST 返回池；循环效果必须由显式 Stop 结束。
- ResetForPool MUST 清理 Particle、Trail、Animator、MaterialPropertyBlock、事件订阅和临时 Transform 状态。
- Trail 重用时 MUST 在定位完成后 Clear，避免跨屏拉线。
- Particle System MUST 设置有限 `maxParticles`。
- 验收时 MUST 使用固定 seed；正式运行是否随机由 Recipe 明确。
- Stop 模式 MUST 区分立即清空和允许尾迹自然结束。

## 9. 2D、2.5D、3D、Screen

| Dimension | MUST |
|---|---|
| 2D | 明确 Sorting Layer/Order、正交相机、像素密度和翻转规则 |
| 2.5D | 明确相机约束、Billboard 轴、深度与 Sorting 混合规则 |
| 3D | 至少正面、侧面、斜视、游戏距离验证；只有单一角度成立不得定版 |
| Screen | 明确 Canvas/Camera 模式、安全区、分辨率缩放和 UI Mask |

2D 与 3D MAY 共享 Recipe 语义，但 MUST NOT 强制共享具体 Renderer 实现。

## 10. AI 边界

- AI MAY 生成 Recipe、Patch、视觉 Brief 和候选纹理。
- AI MUST NOT 直接编辑 Unity YAML、`.meta`、GUID 或任意 C# 类型/属性路径。
- AI 输出 MUST 经过 Schema、Catalog、参数范围、预算和依赖验证后才能 Build。
- AI 生成纹理 MUST 保存来源、提示词、参考图许可/归属、SHA-256 和人工选择记录。
- AI 生成成功不等于视觉通过；最终视觉必须由同一 Runtime Preview 人工审阅。
- 未来运行时 AI 仍只能输出受限语义数据，不直接获得文件系统或 UnityEditor 写权限。

## 11. Managed 与未来 Detach/Bake

- 当前默认全部为 Managed Output；人工改动会被下次 Build 覆盖。
- Detach/Bake 实现前，不得假装 Generated Prefab 可安全手改。
- 未来 Baked Output MUST：复制必要本地依赖、移除 Editor/Recipe 运行依赖、记录来源 Manifest、生成新 GUID，并停止被 Compiler 自动覆盖。
- Detach/Bake 的具体时机作为独立 ADR 决定，本规则只预留边界。

## 12. 变更与例外

- 任何 MUST 变更需要规则版本升级和迁移说明；版本号与变更记录维护在 `README.md` 的版本历史一节。
- 超预算但视觉必要的效果必须记录 waiver：原因、实际成本、目标平台、测试结果、批准人和复查日期。
- 不允许以“看起来没问题”为理由跳过 Missing Script、生命周期、池化或依赖门禁。
- 依赖主观判断的 MUST 条款（如“节点名称表达职责”“Reveal 必须表达扫出过程”）由 Gate 5 人工审阅裁定，审阅者的 `pass / conditional pass / reject` 签署即为最终判定依据。
- Prefab 深拷贝与 Material/Texture 共享边界由 `ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md` 裁定；ADR 状态不是 `Accepted` 时，不得执行迁移 M3。
