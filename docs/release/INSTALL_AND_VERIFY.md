# 安装、打开与运行测试 — 内部 MVP 0.1.0

## 前提

- Windows、Unity **2022.3.62f3c1**（精确路径：`E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`）。
- 项目使用 URP **14.0.12**；不要用 Unity 6 或其他 2022.3 patch 自动升级。
- 打开/批处理前确认没有另一个 Unity Editor 占用 `project/`。脚本会拒绝活动项目锁；陈旧锁诊断只会告警。

## 打开

1. 用上述版本在 Unity Hub/Add 中打开 `D:\WorkWork\Assist\image_to_smart\project`。
2. 等待 Package、URP 和资产导入完成；Package 位于 `project/Packages/com.vfxcomposer.unity`，无需另行安装 registry 包。
3. 2D Preview：`Tools/VFX Composer/Preview`；3D Preview：`Tools/VFX Composer/3D Preview`；Slash v2 Preview：`Tools/VFX Composer/Slash v2 Preview/Open Generated Scene`。固定场景为 `Assets/VFX/Preview/S7_2D_FireballPreview.unity`、`Assets/VFX/Preview/S10_3D_FireballPreview.unity`、`Assets/VFX/Preview/S12_SlashGeneratedPreview.unity`，以及 AI 本地验证场景 `Assets/VFX/Preview/S12_AI_ValidatedSlash/S12_AI_ValidatedSlashPreview.unity`；四者均为 enabled Player Build scenes。

生成物只在 `Assets/VFX/Generated/`；`Assets/VFX/Templates/` 是受保护的只读输入。直接 Inspector 修改 Generated 可用于调试，但下一次 Build 会覆盖；Detach/Bake 不属于 MVP。

## 可复跑门禁

从仓库根执行（不要在图形 Editor 占用项目时执行）：

```bat
cmd /c tools\audit-gitignore.bat
cmd /c tools\compile-check.bat
cmd /c tools\run-tests.bat EditMode
cmd /c tools\run-tests.bat PlayMode
```

结果在 `test-results/`（已忽略）。本次验收的全量结果为 EditMode **141 total / 106 passed / 0 failed / 35 Explicit historical skipped**，PlayMode **8/8**。EditMode 包含正常运行的 S11 A1–A8 与错误码/性能报告防漂移测试；S9 一次性历史证据保持 NUnit `Explicit`，不会伪装为本次回归通过。

## Recipe 到 Prefab

打开 `Tools/VFX Composer/Compiler`，选择正式 Recipe：v1 火球为 `Assets/VFX/Recipes/fireball-2d.default.json` 或 `Assets/VFX/Recipes/fireball-3d.default.json`；v2 Slash 为 `Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json`。窗口会按 `recipeVersion` 自动分发 v1/v2 的 Validate、Dry Run、Build 与 Preview；不要把 v2 Slash 交给旧 v1-only 工作流。

依次执行 Validate、Dry Run、Build、Preview。Build Hash 由规范 Recipe、recipe revision、模板版本/依赖 hash、compiler version 和 Unity version 组成；输入相同则 Dry Run 为 `Unchanged`，不会重写资产。每个输出目录的 `BuildManifest.json` 记录这些可追溯事实。

`S12_AI_ValidatedSlashPreview.unity` 是已保存 AI Recipe 证据的本地播放场景，不是新的 formal Generated Recipe，不能作为新的 Recipe 输入或替代 `slash-3d-stylized.default.v2.json`。AI 不在运行时参与，也不会直接编辑 Unity YAML；其已记录的 Recipe/Patch 结果仅通过受审核的编辑器编译器与事务式 Patch 服务生成。
