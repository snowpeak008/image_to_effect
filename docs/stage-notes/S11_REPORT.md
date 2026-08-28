# S11 阶段纪要：稳定化与内部 MVP 发布准备

> 状态：**主 Agent 独立验收通过；S11/M8 完成。**  
> 日期：2026-08-22  
> 范围：WP8/S11 稳定化、机器验收、发布文档、版本一致性；不新增 Recipe v1 字段、功能、外部发布、Git commit 或 tag。

## 发布版本与边界

- 内部 MVP 为 **0.1.0**：`package.json`、Runtime marker、`VfxCompiler.CompilerVersion`、仓库根 [`CHANGELOG.md`](../../CHANGELOG.md) 和两份正式 Generated `BuildManifest.json` 一致。编译器版本是 Build Hash 输入；Recipe/Manifest 结构版本仍为 **v1**，没有伪造的 v2/迁移。
- 仅保留正式 Generated `fireball_2d` 与 `fireball_3d`。正式 Recipe、模板、S5/S7/S10 金样/证据未被作为测试 fixture 清理或替换。
- 性能文档明确为“静态预检，非真机认证”；PC Editor 是实际目标，`mobile_medium` 是静态 profile，不声称设备帧时间认证。

## A1–A8 机器验收

[A1–A8 matrix](../release/A1_A8_ACCEPTANCE_MATRIX.md) 对应正常非-Explicit 测试，而不是文字替代：真实 2D Build、2D/3D 同输入重建 `Unchanged` + GUID、非法 template/range 的实际 Build 拒绝及 Generated SHA-256、Build/真实 failed Patch 的 Templates SHA-256、隔离 Recipe 的真实 `rate 18→9` Patch revision/history/Prefab 快照、正式 Preview 场景中的保留 Generated Prefab PlayMode、Player Build，以及 3D shared semantic graph 与 `E310` Unsupported。

历史 S9 一次性 Cohort 证据继续保持 `Explicit`，没有被删除、改写或计入发布回归；所有新增 S11 测试正常随 full suite 运行。

## 静态性能与错误码

- [静态性能预检](../release/STATIC_PERFORMANCE_PREFLIGHT.md) 从 live Recipe、Catalog/Manifest 和 Generated Build Manifest 读取 2D/3D cost；普通测试会验证表格数字和两个预算档位，防止文档漂移。
- [错误码审计](../release/ERROR_CODES.md) 列举每个实际 Editor 报告发射码的稳定路径族、人话说明与 actual/allowed 适用性。普通测试从 Editor 源代码提取 `E###`/`I###` 后与表格双向比对；预算 `W401–W404` 是相应 Error 码的固定派生。

## A7 发现并修复的前遗留 Blocker

真实 Windows Player Build 首次失败，原因是 `Assets/VFX/Authoring/S5TemplateAuthoring.cs` 与 `S10TemplateAuthoring.cs` 是 one-shot Editor 工具却位于非 `Editor/` 路径且没有 whole-file guard，Player 编译会解析 `UnityEditor`/`VFXComposer.Editor`。修复为两个文件外层 `#if UNITY_EDITOR`，不移动路径、不改 `.meta`/GUID、不改模板、Recipe 或 Runtime。两个正式 Preview 已纳入 Player Build scene list；Player Build 输出仅写入精确的 system-temp child，`finally` 删除。新增审计拒绝 Assets 非 Editor 路径出现未整体 guard 的 Editor 依赖。

## 文档与操作

- [安装、打开与验证](../release/INSTALL_AND_VERIFY.md)
- [Recipe/Manifest/模板规范](../release/RECIPE_MANIFEST_TEMPLATE_V1.md)
- [升级与迁移边界](../release/UPGRADE_AND_MIGRATION.md)
- [MVP 验收报告](../release/MVP_ACCEPTANCE_REPORT.md)
- 既有 [AI workflow](../ai-workflow/README.md) 是 Recipe/报告/Patch 作者入口。

## 开发侧最终门禁记录

本开发侧在无活动 Unity 项目进程的 batch 环境完成以下运行；这不是主 Agent 独立验收，也不宣称 M8：

| 门禁 | 开发侧结果 |
| --- | --- |
| `cmd /c tools\audit-gitignore.bat` | 通过；正式 Package/Template/ProjectSettings 可追踪，缓存/Build/test-results 被忽略。 |
| `cmd /c tools\compile-check.bat` | exit 0。 |
| `cmd /c tools\run-tests.bat EditMode` | 116 total / 81 passed / 0 failed / 35 historical Explicit skipped。包含真实 Windows Player Build（system temp 输出、finally 清理）。 |
| `cmd /c tools\run-tests.bat PlayMode` | 6 total / 6 passed / 0 failed。包含两固定场景中保留正式 Generated Prefab 的实际播放。 |
| 清理 | Generated 仅 `fireball_2d`、`fireball_3d`；未发现 `s11_` Recipe/history、`vfxs6tmp`、pending、backup 或 system-temp Player 输出；最终无 Unity 进程。 |

## 主 Agent 独立验收

2026-08-22 21:38:45 +08:00，主 Agent 在开发 Agent 完成后独立执行并核验：Git-ignore audit 通过；compile exit 0；EditMode 117 total / 82 passed / 0 failed / 35 intentionally Explicit historical tests skipped；PlayMode 6/6 passed。13 个 S11 发布测试全部实际执行，其中 Windows Player Build 成功写入并清理 system-temp 输出；正式 2D/3D Preview 场景中的 Generated Prefab 完成 Launch、Travel、Impact 与 Stop。

独立只读审计同时确认：65 个实际报告码与错误码文档双向一致且表格字段完整；发布 Markdown 本地链接零断链；正式文件均可被 Git 追踪；两个正式 Prefab 的 GUID 仍被 Preview 场景引用；Generated 仅含 `fireball_2d`、`fireball_3d`；S11 Recipe/history/temp/pending/backup、Player 临时输出和 Unity 项目进程均为零。Gate E 条件成立，M8 判定 **GO**。这表示内部 MVP 0.1.0 可供试用，不表示已创建 Git tag、registry 包、外部发布或真机性能认证。
