# VFX asset boundary

`Templates/` is the versioned, read-only input boundary for formal template Prefabs and manifests. `Generated/` is the managed output boundary: only VFX Composer tooling may create or update assets there. S1/S2 spike templates, generated Prefabs, and runners must never be copied into either directory.
# VFX 生产资产边界

正式特效必须通过 `Packages/com.vfxcomposer.unity/Editor/Rules` 中的统一门禁。项目规则的机器配置位于
`ProjectSettings/VFXComposer/VfxProjectRules.json`，人工规范位于仓库根 `docs/rules/`。

- 新 EffectId 默认使用 `strict`：结构、依赖、缺失引用、Preview/Editor 组件、所有权 Manifest 任一硬门禁失败时不得提交成品。
- `fireball_2d`、`fireball_3d`、`slash_3d_stylized` 是迁移前遗留成品，只做 `legacy_audit`；这不代表它们已经满足新目录和共享资源规则。
- 权威新 Manifest 写入 `ProjectSettings/VFXComposer/BuildManifests/`，不作为 Player 资产。`Generated/*/BuildManifest.json` 仅为旧编译器兼容读入口。
- 规则 Manifest 的 `ownedOutputs[]` 才授予构建器替换/未来 stale 清理所有权；`dependencies[]` 只记录引用，绝不授权删除共享资产。
- ADR-001 未 Accepted 前，禁止改变 Prefab 深拷贝、Material/Texture 共享迁移策略。

可在 Unity 使用 `Tools > VFX Composer > Production Rules > Reconcile Current Outputs` 重建三项遗留审计 Manifest。

新的严格示例 `frost_impact_2d` 使用 `Tools > VFX Composer > 2D Impact > Build Frost Impact + Preview` 构建；其 Generated 目录只允许一个 Runtime Prefab，纹理和材质来自 `Assets/VFX/Shared`。
