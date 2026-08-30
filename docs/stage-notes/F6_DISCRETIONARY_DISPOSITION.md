# F6 裁量项处置

> 主计划 F6 卡「裁量项（时间允许则做，否则逐条记录不做理由）」。用户裁定：全做。逐条处置如下。

| 项 | 来源 | 处置 | 说明 |
|---|---|---|---|
| 空目录来源 | F2 审计③ | **已查明** | `Assets/VFX/Shared/Materials\|Textures` 系 Unity 编辑器/构建创建的空目录，随还原纪律 `git clean` 清除，非写入面隐患。见 `F6_E2E_EVIDENCE.md`。 |
| 注释过时 | F2 审计④ | **已修** | `InlineManifestRecipeProbe` 注释已在 `a23c6b6c` 订正。 |
| WriteResult 结果路径项目外 | F2 审计⑤ | **已核（inspection）** | `tools/Invoke-Unity.ps1` 在 listener 前拒绝 request/result 落在 project 边界内（`$projectBoundary` 前缀比对，exit 64）；入口只写调用方给定的项目外结果路径。真机 E2E 中结果文件位于 `.codex_tmp/`（项目外）验证通过。无需改码。 |
| `single-` 前缀 | F5 审计③ | **已注释降级** | `DeriveSingleBatchId` 增注释：`single-` 是可读性派生约定、非保留命名空间，用户 batchId 撞名不拒绝，SHA 派生后缀使真实碰撞可忽略。 |
| 派生清单 onFailure 断言 | F5 审计④ | **已补测** | `GenerateEffectEnqueuesOneEntryUnderADerivedBatch` 增断言：派生单条清单 `onFailure=continue`、入队 `BatchPolicy=CONTINUE`。 |
| MCP Ctrl+C 假象 | F5 审计⑦ | **已订正注释** | `Program.cs` 注释纠正：Ctrl+C 结束本 stdio 会话；本 server 只入队，已入队 job 在拥有执行器的宿主进程 drain 时才跑，不「在本进程下」运行。 |
| initialize 必需成员 | F5 审计⑧ | **已收紧** | `McpServer` 握手改为强制 `protocolVersion` 存在且合规（原「存在才校验」，缺失即放行）。 |
| 常量重复 | F5 审计⑨ | **已复核，无动作** | MCP 内重复字面量均为手写 JSON writer 的协议键（`type`/`batchId`/`jobId`…），逐处内联是惯用写法，抽共享常量反损可读性；未发现值得去重的具名常量重复。 |
| ValidationFailed/ChannelFailed 区分码 | F4 审计④ | **已做** | 新增 `VFXJ0017 GenerationValidationExhausted`、`VFXJ0018 GenerationChannelFailed`（注册进闭集目录），`RecipeGenerationJobExecutor` 按 `result.Outcome` 分派；6 处生成失败测试按精确场景更新（校验耗尽/通道失败/载荷解析仍 ExecutionFailed/通道取消经 OperationCanceled 仍 ExecutionFailed）。 |
| Desktop 裸 catch 稳定码 | F1 审计② | **已做** | `CreateViewModel` 两处末尾裸 `catch` 增稳定码 `VFXUI001`，与同族带 `exception.Code` 的 catch 对齐。 |
| 执行器锁跨进程真杀测试 | F3 审计3 | **已做（D3，见下）** | 原按裁量记录不做；用户「都做了」裁定后于 `task/deferred-cleanup` 分支交付。 |

全量 Release 测试：0 warning/0 error，**745/745**（较基线 733 +12）。

---

## 延后清算四项（`task/deferred-cleanup` 分支，用户「都做了」裁定）

主 agent 崩溃恢复续接后，四项此前明确延后/记录不做的清算项全部交付并逐项验证。

| 项 | 来源 | 提交 | 验证 |
|---|---|---|---|
| **D1** RestoreProvenance 原子回滚 | F6 已知限制⑥ | `5565149d` | `existed` 分支改 `.pending`+原子替换（复用 `ReplaceWithBoundedRetry`），与 `TryWriteProvenance` 写入纪律一致；`RestoreProvenance` 提 internal，补此前**零覆盖**的回滚路径 EditMode 测试（原子换字节且不留 `.pending`／未存在则删 recipe+meta）。EditMode `VfxRecipeBuildEntrypointTests` 28/28。 |
| **D2** Jobs 页批次分组折叠 | F3 审计5 | `23de31dd` | 按 batchId 折叠为可折叠分组，折叠态跨刷新保留；`JobBatchGroupViewModel` + `Batches`/`RebuildBatches`。Desktop.Tests 39/39。 |
| **D3** 执行器锁跨进程真杀测试 | F3 审计3 | `365857b5` | 仿 `RevisionLockHost` 建独立宿主可执行（`hold-executor`）：真进程占锁→跨进程互斥被拒（`ExecutorLockUnavailable`）→Kill 后新宿主接管。Jobs.Tests 57/57。 |
| **D4** dependencyHash 移出入库清单 | §160 后续债务 | `9c5e5ace` | `VfxDependencyRecord` 删字段、`VfxBuildTemplate` 加 `[JsonIgnore]`（保留 buildHash 语义）、formal reader 校验放宽；机械迁移 229 个入库清单（纯 dependencyHash 去除、无时间戳漂移）；S12A/S12B 冻结门哈希经真 .NET 复算重定值。EditMode 全量 686/686。 |

复验（分支尖端 `9c5e5ace`）：**EditMode 全量 686/0/54**（54 跳过含 R-4 显式豁免）；**.NET Release 全 11 测试工程 0 失败**（sln 级 9 工程 715 通过 + 独立 `Client.Tests` 16 + `Broker.ServiceHost.Tests` 16 = 747）。待用户批准合并 master + 推送。
