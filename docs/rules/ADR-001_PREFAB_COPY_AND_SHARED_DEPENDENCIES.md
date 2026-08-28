# ADR-001：Prefab 深拷贝与共享依赖边界

状态：`Proposed`（待决策人签署）  
日期：2026-08-23  
影响阶段：M3（当前冻结）  
决策人：待填写

## 1. 背景

S1 Spike 对两种 Prefab 组装方式做过实际验证：

- 深拷贝/Unpack 后，模板变化不会在未 Build 时传播到 Generated Prefab，能够维持“显式 Build 才改变成品”的边界；代价是文件体积较大。
- Nested Prefab 会让未被 override 的模板属性在未 Build、Recipe/build hash 未变化时传播到 Generated 成品。

因此 `docs/DECISIONS.md` 将正式组装策略定为深拷贝。

新的全项目规则希望 Shader、Material、Texture、Noise、Atlas 和基础 Mesh 尽量共享，以减少每个 EffectId 的重复文件。这里存在需要澄清的边界：S1 的结论主要针对 **Prefab 结构组装**，新规则主要针对 **资源依赖所有权**；两者可能共存，也可能因共享 Material 的隐式传播重新引入 S1 风险。

本 ADR 未签署前不改变既有决定，M3 保持冻结。

## 2. 必须裁定的问题

1. Compiler 组装 Runtime Prefab 时继续完全深拷贝，还是允许 Nested Prefab/Prefab Variant？
2. Material、Texture、Shader 和 Mesh 哪些可以共享，哪些必须 Effect-local？
3. 共享资产发生变化时，如何保证 dependency hash、Build Plan 和视觉审批状态同步失效，而不是静默传播？
4. 现有 Fireball/Slash Managed Output 如何迁移，同时保持 Runtime Prefab GUID、回滚能力和引用安全？
5. “Prefab 结构深拷贝 + 不可变、版本化 Shared Dependency”是否作为允许的混合方案？

## 3. 不可违反的共同约束

无论选择哪个方案，都 MUST：

- 保持“成品变化可追踪”；Recipe、Compiler 或递归依赖变化必须改变 Build/dependency hash。
- 不在未记录依赖版本的情况下静默改变已批准视觉。
- 保持正式 Runtime Prefab GUID。
- 失败时恢复资产文件、Manifest、GUID 和引用。
- 区分 owned output 与 shared dependency；一个 Effect 不得删除 Shared 资产。
- 对相同输入保持幂等。
- 迁移前生成直接/递归引用清单。

## 4. 方案 A：继续全深拷贝

Runtime Prefab 结构、必要 Material 和可变依赖均复制到 Effect-local 输出。

优点：

- 延续 S1 已验证边界；模板变化不会绕过 Build。
- 单个成品自包含，回滚直观。

代价：

- Material/Mesh/Texture 可能大量重复。
- 修复共享 Shader/纹理问题需要重建多个 Effect。
- 仓库、导入和内存审计复杂。

## 5. 方案 B：结构深拷贝，共享依赖不可变且版本化

Compiler 继续深拷贝/解包 Prefab 结构；Shader、Material、Texture、Noise、Atlas 和基础 Mesh 可引用版本化 Shared Asset。共享资产批准后视为不可变，破坏性修改创建新版本；递归 dependency hash 进入 Build Hash。

优点：

- 保留 S1 对 Prefab 结构的显式 Build 边界。
- 大幅减少资源副本。
- Shared 修复可通过新版本和受控迁移传播。

风险：

- Unity 仍允许人工编辑旧 Shared Asset；需要只读约定、审计或工具门禁。
- 仅 dependency hash 改变不能阻止画面在 Build 前已经随 Shared Asset 改变；若要求“批准画面绝不静默变化”，还需要版本目录不可变、写保护或构建/提交检查。
- MaterialPropertyBlock 与 Instancing 属性必须严格区分。

## 6. 方案 C：Nested Prefab/Variant + 共享依赖

Generated Effect 保留到 Template/Nested Prefab 的连接，通过 overrides/Variant 表达差异。

优点：

- 输出体积最小；公共结构修改方便。
- 更符合手工 Prefab Variant 工作流。

风险：

- 与 S1 已观察到的未 Build 隐式传播直接冲突。
- Recipe/build hash 可能暂时不能解释当前画面。
- 对 AI/事务式 Compiler 的可追溯性要求最高。

采用本方案需要明确推翻 `docs/DECISIONS.md` 的深拷贝决定，并提供新的隔离和回滚实测证据。

## 7. 初步兼容性判断（非决策）

“Prefab 结构深拷贝”和“Material/Texture 共享”在技术上可以共存，但只有当 Shared Asset 具备不可变版本和完整递归 dependency hash 时，才不会简单重演 S1 的隐式传播问题。dependency hash 只能检测变化，不能单独阻止 Unity 引用立即看到变化，因此还需要共享资产变更控制。

本判断不等于批准方案 B。

## 8. 决策前所需证据

- 对 A/B/C 各构建一个最小 Effect，记录 Prefab/Material/Texture 依赖和文件体积。
- 在不 Build 的情况下修改 Template、Shared Material、Shared Texture，记录运行画面是否改变。
- 复算 dependency/build hash，证明变化被检测。
- 注入构建失败，验证 Generated、Manifest、GUID 和 Shared 不被破坏。
- Player Build 比较重复资产、内存和 Draw Call。
- 验证 B 的 Shared 版本不可变门禁能实际阻止原地破坏性修改。

## 9. 决策记录（待签署）

```text
Decision: A / B / C / Other
Effective rule version:
Supersedes docs/DECISIONS.md row:
Migration requirements:
Waivers:
Decision owner:
Signature/date:
```

只有本节填写并将状态改为 `Accepted` 后，M3 才能解冻。
