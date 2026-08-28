# 40 当前项目差距与迁移

状态：只读审计与迁移提案；未授权删除、移动或重建现有资产。

时效声明：本文件基于 2026-08-23 的仓库快照，属于一次性迁移提案而非长期规则；文中数字不随仓库演进更新。M1–M5 全部完成后本文件应归档（移出 `docs/rules` 阅读顺序），后续以各阶段的实际审计报告为准。

## 1. 当前范围

正式可运行效果：

- `fireball_2d`：Projectile / 2D。
- `fireball_3d`：Projectile / 3D。
- `slash_3d_stylized`：Slash / 3D。

当前尚未形成正式独立 Archetype：

- Standalone Impact；
- Aura；
- Area；
- Beam。

2D Fireball 的 `Impact Only` 是 Projectile 内阶段预览，不等于 Standalone Impact 成品。

## 2. 当前物理规模快照（2026-08-23）

| 区域 | 文件数/规模 | 结论 |
|---|---:|---|
| `docs` | 686 文件，约 16.8 MB | 研究证据过多，需归档策略 |
| `project/Assets` | 339 文件；152 个非 `.meta` | 正式资产与 Authoring/Preview 混杂 |
| UPM package | 232 文件；109 个非 `.meta` | Runtime/Editor/Tests 分区基本正确 |
| `test-results` | 163 文件，约 14.3 MB | 应视为 artifacts，不长期累积 |
| `spike` | 33,675 文件，约 2.35 GB | 主要为一次性 Unity 工程缓存；不应作为日常工程内容 |

## 3. Slash 快照

`Assets/VFX/Templates/3D/Slash`：

- 37 个非 `.meta` 文件，约 5.95 MB；
- 19 Mesh；
- 6 Material；
- 5 Template Prefab；
- 4 Texture；
- 3 Shader。

`Assets/VFX/Generated/slash_3d_stylized`：

- 1 Runtime Prefab；
- 4 Material 副本；
- 1 Build Manifest；
- 约 389 KB。

Generated Prefab：

- 17 GameObject；
- 4 MeshRenderer；
- 3 ParticleSystemRenderer；
- 3 ParticleSystem；
- 5 MonoBehaviour。

判断：Preview Scene 顶层 `Camera + Prefab + Driver` 合理；主要冗余来自 Prefab 内阶段容器/helper 节点，以及 Templates 中 S12 与 S15 历史资产同时活跃。

## 4. 迁移原则

- 先引用审计，后移动/删除。
- 保留所有正式 Prefab GUID。
- 第一轮只改变结构和资产所有权，不改变视觉、时间线、Recipe 参数和相机。
- 任何视觉像素变化超过批准阈值即回滚结构迁移。
- 不删除无法证明归属和无引用的用户文件。
- `docs/standards/UNITY_VFX_ASSET_SPEC_V1.md` 是前一版 Slash 导向草案；本目录规则批准后将其标记 superseded，不直接删除。

## 5. 建议迁移阶段

### M1：只读 Inventory

产出每个资产的：GUID、类型、大小、直接/递归引用者、是否被 Player 使用、最后生成阶段、建议动作。

动作标签：

- KEEP_SHARED
- KEEP_LOCAL
- MERGE_SUBASSET
- MOVE_EDITOR
- ARCHIVE_EVIDENCE
- DELETE_STALE
- UNKNOWN_REVIEW

M1 不修改任何文件。

### M2：Slash 结构瘦身验证

目标：

- 17 GameObject → 8–10；
- 深度不超过 2；
- 移除空阶段容器；
- Helper 组件迁移到根/Renderer；
- 三个 painted layer Anchor 仍为 0 px 偏差；
- Runtime Prefab GUID 不变；
- 最终 0.45s 动画与批准版视觉等价。

只运行 targeted compile/EditMode/PlayMode 和真实 Preview 录制；用户视觉确认后才进入 M3。

### M3：共享资产与 Mesh 合并

状态：**FROZEN，等待 ADR-001 获得决策人签署并变为 `Accepted`。** ADR 未裁定前不得移动共享依赖、改变 Prefab 组装策略或移除材质深拷贝。

- 标识实际在用的 S15 纹理/Shader/Material/Mesh；
- 合并独占程序化 Mesh 到一个 Data Asset；
- 将可共享 Shader/Material/Noise/Atlas 移入 Shared；
- 移除每效果无条件材质深拷贝；
- Dependency Hash 覆盖共享版本；
- 用版本化共享资产避免模板静默传播。

### M4：旧资产与证据归档

- 生成删除前反向引用报告；
- 用 Unity AssetDatabase 移动/删除，保留 `.meta` 关系；
- S12 旧资产、rejected runs、旧 AI cohort、测试日志移出活动区；
- 清理 spike 的 Library/Temp/Logs 等可再生缓存；
- 报告释放空间及不可恢复内容。

此阶段具有删除性质，必须再次取得用户明确授权。

### M5：全项目门禁自动化

- 对 Fireball 2D、Fireball 3D、Slash 执行统一资产审计；
- 定义跨 Archetype 统一 Recipe Schema、Build Manifest Schema、v1/v2 迁移与 Dispatcher 兼容测试；
- Compiler 在 Commit 前拒绝超结构预算、重复依赖和 stale output；
- 增加对象池 100 次复用测试；
- 完整 EditMode、PlayMode、Player Build；
- 输出统一验收报告。

## 6. 下一轮成功标准

下一轮只以 Slash 证明规则可实施：

- 用户看到的动作与当前批准标准同步；
- 起点锚点最大偏差仍为 0 px；
- GameObject 数量进入 8–10；
- 正式 Prefab GUID 不变；
- 不新增 Shader/Texture/Material 副本；
- 构建两次文件集合相同；
- 失败注入可以恢复；
- 用户批准后才删除旧资产。

通过后再把相同规则应用到 Fireball 2D/3D，并以 Standalone Impact 作为第一个从一开始就按新规格开发的 Archetype。
