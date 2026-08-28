# VFX Composer 全项目规则集

版本：`1.0-draft`  
状态：用户已批准接入；`1.0-draft` 机器门禁已启用，ADR-001/M3 仍冻结  
适用范围：Unity 2022.3 LTS + URP 14；2D、2.5D、3D；人工制作、Recipe/Patch 编译、AI 辅助生成、未来运行时 AI 生成。

本目录是全项目的规范入口。任何旧计划、阶段报告或示例若与这里冲突，应先提出变更申请。机器配置、统一审计和 Manifest 实现位于 `project/ProjectSettings/VFXComposer/` 与 `project/Packages/com.vfxcomposer.unity/Editor/Rules/`。

执行边界：

- 新 EffectId 默认 `strict`，硬门禁失败不得留下可交付成品。
- 现有 `fireball_2d`、`fireball_3d`、`slash_3d_stylized` 标为 `legacy_audit`，仅代表可继续运行和生成迁移报告，不代表已经符合新目录、命名和共享资源规则。
- 权威所有权 Manifest 写入 `project/ProjectSettings/VFXComposer/BuildManifests/`；旧 `Assets/VFX/Generated/*/BuildManifest.json` 暂作旧编译器兼容读入口。
- ADR-001 未签署为 `Accepted` 前，机器门禁会阻止新的不合规成品，但不会擅自执行深拷贝/共享资源迁移或删除现有资产。

## 阅读顺序

1. [全局核心规则](00_CORE_RULES.md)
2. [特效类型配置](10_ARCHETYPE_PROFILES.md)
3. [目录、文件与命名](20_ASSET_LAYOUT_AND_NAMING.md)
4. [视觉模块与 Atlas 生产流程](25_VISUAL_MODULE_AND_ATLAS_WORKFLOW.md)
5. [验收、性能与交付](30_ACCEPTANCE_AND_DELIVERY.md)
6. [当前项目差距与迁移](40_CURRENT_PROJECT_MIGRATION.md)
7. [ADR-001：Prefab 深拷贝与共享依赖边界（草案）](ADR-001_PREFAB_COPY_AND_SHARED_DEPENDENCIES.md)
8. [ADR-002：独立 Desktop、Unity Worker 与 Broker](ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md)
9. [ADR-003：Unity Worker 共享协议兼容边界](ADR-003_UNITY_WORKER_PROTOCOL_COMPATIBILITY.md)
10. [机器门禁实现](50_MACHINE_ENFORCEMENT.md)
11. [全项目开发经验库](60_ENGINEERING_LESSONS.md)
12. [视觉迭代、证据与经验递归](70_ITERATION_EVIDENCE_AND_LEARNING.md)

## 规则层级

- `MUST`：硬性门禁，不满足不得作为正式成品。
- `SHOULD`：默认要求；偏离时必须在 Build Manifest 中记录理由。
- `MAY`：允许选择，不构成默认承诺。

## 覆盖策略

规则不把所有特效强行套进 `Launch / Travel / Impact`。每个特效由以下四个轴描述，因此新类型也能纳入：

- 生命周期：瞬时、一次性时间线、持续、循环、事件驱动。
- 空间拓扑：点、方向弧、移动体、线段、区域、包围体、屏幕空间。
- 挂载方式：世界、施法者、目标、骨骼、投射物、相机。
- 渲染后端：Sprite、Particle System、Mesh、Trail/Line Renderer、Shader、可选 VFX Graph。

当前明确覆盖 Projectile、Impact、Slash、Aura、Area、Beam、Trail、Shield、Spawn/Transform、Environment 和 Screen/UI VFX；未列出的新 Archetype 仍先遵守全局核心规则，再补一页差异配置。

## 审阅重点

- 是否接受最终运行目录从“无限追加 Generated”转为“一个 EffectId 一个收敛成品包”。
- 是否接受普通特效 `10` 个 GameObject、复杂特效 `16` 个 GameObject 的项目预算。
- 是否接受 ADR-001 对 Prefab 组装与 Material/Texture 共享边界的最终裁定；ADR 签署前冻结 M3。
- 是否接受只保留最终一轮日常视觉证据，历史研究证据移出 Unity 活动目录。
- 是否接受下一轮先做 Slash 结构迁移验证，并要求视觉和 GUID 不变。

## 版本历史

规则集整体使用单一版本号；任何 MUST 级变更必须升级版本并在此登记（`00_CORE_RULES.md` §12）。

| 版本 | 日期 | 变更 |
|---|---|---|
| `1.0-draft` | 2026-08-23 | 初稿；同日修订：预算表归类补全、Gate 顺序对齐、Projectile v1/Slash v2 权威来源拆分、Runtime Entry 抽象、Manifest 最小字段、性能指标可测性、Local/Shared 命名区分、ADR-001 草案、链接统一 2022.3；用户批准后接入机器配置、统一产物审计、外置所有权 Manifest 与 v1/v2 编译门禁，旧三项保留 legacy audit；新增跨 Archetype 的视觉模块拆解、ArtSource/Runtime 边界、Family Atlas、变体复用、裁边定尺、平台压缩和 Gate 0/1/3/5/6 规则；新增可选择的 `preview/compact/balanced/high/custom` 导出规格，强制区分视觉世界尺寸、源码文件、Build 磁盘和 GPU 驻留成本；新增“最大合理拆解”原则；根据 Frost Impact Revision 2–3 复盘新增全项目经验库，明确逻辑拆分与视觉连续性分离、环形资源几何/着色双重闭合、Compiler 行为失效、序列化残留依赖、视觉优化 A/B 和已知失败机器化规则。2026-08-24 追加能力层经验：保护测试使用子集语义、历史隔离 ID 显式规则代际、非法组合使用完整合法 fixture、Manifest 范围优先、Preview Scene 只依赖自有稳定标识。 |

`40_CURRENT_PROJECT_MIGRATION.md` 是基于当日快照的迁移提案，不随规则版本长期维护；M1–M5 完成后应整体归档并从阅读顺序中移除。
