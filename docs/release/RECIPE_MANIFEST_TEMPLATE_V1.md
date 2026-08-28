# Recipe v1、Manifest v1 与模板制作规范

## Recipe v1（冻结）

权威详细 schema 在 [AI 工作流 schema](../ai-workflow/recipe-v1.schema.json)，作者说明在 [Recipe authoring](../ai-workflow/recipe-authoring.md)。Recipe v1 根字段为 `recipeVersion: 1`、`revision >= 1`、`id`、`dimension`（`2d`/`3d`）、`archetype: projectile`、`targetProfile`、`randomSeed`、`stages`、`metadata`。阶段用稳定 ID（`launch`/`travel`/`impact`）；模块同样使用稳定 ID、语义 `kind`、`templateId`、受 Manifest 限制的 `parameters`、同阶段 `attachTo` 与 `enabled`。

Recipe 不得包含 Unity 属性路径、Shader、任意磁盘路径、C# 类型、物理/伤害/碰撞或跨阶段 attach。未知字段是错误而不是忽略。Patch 只接受裸操作数组、稳定 ID 路径及 `replace/add/remove/enable/disable`，完整语法见 [Patch authoring](../ai-workflow/patch-authoring.md)。

## Template Manifest v1

每个 `Assets/VFX/Templates/{2D,3D}/Manifests/*.manifest.json` 都需要：`manifestVersion: 1`、唯一 `templateId`、`templateVersion`、`kind`、`dimension`、可解析的 `assetGuid` 与规范 `assetPath`、tags、parameters 和 cost（estimatedPeakParticles/materials/trails）。每个参数声明 `type`、`min`、`max`、`default` 和**符号化** `binding`；Compiler 只接受注册表 allow-list，绝不从 Manifest 反射任意 Unity 属性。

模板制作/修改流程：先在 Templates 内制作 Prefab 并独立播放；按 min/default/max 进行可见性、标准表现和稳定性三点检查；填写成本、已知边界及 v1 Manifest；运行 full suite；最后由 Recipe Build 生成新的 Managed 输出。不要手改 Templates 来“修复” Generated，也不要把生成材质放回 Templates。

参数删除、类型/范围语义更改或 template ID 重用是破坏性 Manifest 变化，必须先完成兼容性设计与版本/迁移评审。纯视觉调整但参数契约不变可提高模板小版本。

## 版本语义

- `recipeVersion` 与 `manifestVersion` 均冻结为 **1**；它们表示结构契约，不随包补丁变化。
- Package 与 `VfxCompiler.CompilerVersion` 为内部 MVP **0.1.0**。Compiler version 进入 Build Hash；升级 compiler 会将已有输出标为需重建，并把新值写入 Build Manifest。
- Unity 版本也进入 Build Hash；Unity 大版本升级是独立迁移，不是普通 Build。
