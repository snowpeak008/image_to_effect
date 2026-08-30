# F6 端到端验收证据

> 日期：2026-08-30　|　分支：`task/F6-e2e-acceptance`　|　性质：机器可复跑证据（视觉/资产判定仍归用户）

## 流程一：对话生成单特效 → 受限构建 → 三件套写入面（真机 Unity）

**真实 Unity 2022.3.62f3c1 batchmode 构建**，经生产包装 `tools/Invoke-Unity.ps1 -Mode RecipeBuild`
（→ `VfxRecipeBuildEntrypoint.BuildConfirmedRecipe`，`-batchmode -executeMethod`）。输入为
`batches/recipes/spark_projectile_2d.json`（strict 预算：3 stage 根、仅 travel 一个渲染模块、无
`attachTo` 链），请求/结果文件置于项目外。

结果（`recipe-build-result/1`）：

| 字段 | 值 |
|---|---|
| succeeded / 退出码 | `true` / `0` |
| effectId | `spark_projectile_2d` |
| dryRunState | `Create`（首次构建） |
| recipeHash | `e41e3699…f456`（**与 .NET `RecipeCanonicalJson.ComputeSha256` 逐字节一致**，证明双端规范化等价） |
| buildHash | `c80daf65…63fd` |
| compilerVersion / unityVersion | `0.1.0` / `2022.3.62f3c1` |
| catalogIdentityHash | `be783dfa…bb48` |
| budget preflight | `I400` Info，`mobile_medium` 通过 |

三件套写入面（ADR-007）全部落盘、**零越界**：

- `Assets/VFX/Generated/spark_projectile_2d/VFX_spark_projectile_2d.prefab`（+ 同目录 `BuildManifest.json`）
- `ProjectSettings/VFXComposer/BuildManifests/spark_projectile_2d.manifest.json`（权威单点）
- `Assets/VFX/Recipes/spark_projectile_2d.json`（构建溯源）

`git status project/` 除上述成员及其 `.meta` 外无任何改动；无 `dependencyHash` 漂移。构建后按
**还原纪律** `git checkout -- project; git clean -fd project` 还原，未入提交。还原顺带清除了空目录
`Assets/VFX/Shared/Materials|Textures`——即 F2 审计建议③所问的空目录来源：Unity 编辑器/构建创建的
空 Shared 子目录，非写入面隐患。

> 说明：受限构建入口只在含 `com.vfxcomposer.unity` 包的仓库 `project/` 可跑；用户的
> `spike/image_to_smart`（同为 2022.3.62f3c1）不含该包，是用户手动测特效/最终视觉验收的工程，
> 不参与构建入口。视觉判定由用户最终统一签署。

## 流程二：CLI 清单批量 → 队列串行 → 报告/退出码 + MCP 冒烟

机器全覆盖，无 Unity、无 provider、无网络、不触碰项目：

- CLI 经真实 `CliRunner` 走完 run→串行队列→逐条 drain→报告/退出码（`CliRunnerTests`，含
  `TheCheckedInSampleManifestRunsGreenThroughTheRealRunner` 直接消费入库样例 `batches/sample-batch.manifest.json`）。
- MCP submit/status/cancel 往返（`McpToolSurfaceTests`）。
- 人工/provider-backed 复跑步骤见 `eng/run-f6-e2e-acceptance.ps1`。

## 全量机器基线

Release 0 warning / 0 error；.NET 全量 **745/745**（较基线 733 +12：F6 新增测试）。备注：全解并行跑
偶发 1 例 MCP stdio 时序 flake（既有、单跑必绿）。
