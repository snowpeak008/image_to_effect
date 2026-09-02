# 开发记忆与恢复指南

> **在途状态快照（2026-09-03 凌晨）**：
> **F8b4 已关闭合入**：合并 `06f46cd4`，合并态 Release 0/0、全量 **1075/1075**（AI.Tests 294、Desktop.Tests 173）。**当前基线：1075 条 Release 0 失败。** §7 十卡仅剩 F8c（构建闭环）与收尾。worktree `D:\wt\i2s-f8b4` 已退役。
> **新登记 flake**：并行全量下 `Broker.Tests` 的 `UserModeBrokerWorkerSessionTests.PostAdmissionChildExitAutomaticallyRevokesAndCleansSession` 偶发 1~2 条失败（进程时序类，单跑必绿，master 既有非 F8b4 引入）；与 Mcp `ValidatingAManifestOpensNoNetwork`、AiLocalE2E 负载 flake 同列。遇到即单跑确认。
> **用户裁定的收尾路径（2026-09-02 晚）**：①关闭本轮（F8b4 ✓ → 专业模式里程碑审计 → F8c → 收尾）→ ②**批量生成 9 个特效（九宫格）供用户人工验收质量** → ③验收后再议"模板库扩充 track"（当前仅 6 模板/11 参数/2D 火系弹道，覆盖面窄是已登记债务，扩充需用户美术方向输入）。
> **下一步：专业模式里程碑审计**（F8b1+F8b2+F8b3+F8b3b+F8b4 合一次独立审计，拆机械/语义两组；机械组顺带复核三个已登记 flake）。之后 F8c 按 ADR-008（`apps/VFXComposer.BuildHost` 独立宿主子进程、draft-backed 断链接通、Desktop 应用内构建），F8c 后九宫格批量生成可走应用内构建或既有 `vfxc batch run`。
>
> **历史快照（2026-09-02 深夜）**：
> **F8b3b 已关闭合入**：合并 `d974118c`，合并态 Release 0/0、全量 **993/993**（Desktop.Tests 137）。**当前基线：993 条 Release 0 失败。** worktree `D:\wt\i2s-f8b3b` 已退役。专业模式 UI 现有：参数面板（F8b3）+ 版本链视图与回退（F8b3b）；简单模式全量在位。
> **下一步：派发 F8b4 精修回路**（依赖 F8b1 ✓、F8b2 ✓、F8b3 ✓；卡见主计划 §7）。F8b4 范围：每轮 1+N 精修（三件套上下文 + 艺术家知识片段 `refine-artist-knowledge`——源文档 `docs/ai-workflow/refine-artist-knowledge.md` + 入库 fragment JSON，REQ-004 §10 章程）、覆盖守卫（§9.3，别名词表点名判定、guardRestorations 落盘与呈现）、模式切换 UI + ui-preferences `/2` 升版（§5.3，REQ-004-08~11）、任务时间线条目（REQ-004-20 与 -33 时间线半边）；顺带 F8b3b 余项 N7（AI 生成/示例卡路径校验框统一走 L1.5）。F8b4 是唯一动生成链路的卡，交付后做专业模式里程碑审计（F8b1+F8b2+F8b3+F8b3b+F8b4 合一次）。
> **模型槽位勘误**：子 agent 模型槽位名已由 `claude-fable-5-1-thinking-high` 变为 **`claude-fable-5-high`**（本日晚间起）；派发时用新槽位，旧名会被拒绝。
>
> **历史快照（2026-09-02 晚）**：
> **F8b3 已关闭合入**：合并 `f5e776ef`，合并态 Release 0/0、全量 **979/979**（AI.Tests 255、Desktop.Tests 123）。**当前基线：979 条 Release 0 失败。** 简单模式+参数面板闭环（AI 出一版→手调→确认）已有测试。REQ-004 §11.6 已登记 VFXE 码与 F8b3 裁决。worktree `D:\wt\i2s-f8b3` 已退役。
> **下一步：派发 F8b3b 版本链视图与回退**（依赖 F8b3 ✓；卡见主计划 §7）。F8b3b 顺带：B#6 手改被拒时 `RecipeValidationSummary` 改中性键；`RecipeParameterEditResult.Issues` XML doc 补"L1 非 Error 发现"。之后 F8b4（精修回路 + 覆盖守卫 + 模式切换 + ui-preferences `/2` + 时间线）→ 专业模式里程碑审计（F8b1~F8b4 + F8b3b）→ F8c → 收尾。
> 运维追加：全量并行跑时若 AI.Tests 耗时远超 40 s，AiLocalE2E 可能出现 1 条负载 flake（本日合并态首跑出现、复跑绿、未定位用例）；遇到即单跑该工程确认再复跑全量。
>
> **历史快照（2026-09-02 傍晚）**：
> **F8b2 已关闭合入**：合并 `36c2b4f1`，合并态 Release 0/0、全量 **921/921**（AI.Tests 210、Desktop.Tests 110）。**当前基线：921 条 Release 0 失败。** REQ-004 已同步 F8b2 裁决（§7.3 Superseded 澄清、§7.4 第 5/6 条、§7.5 级 2 收紧、RG-6/O-5 关闭）。worktree `D:\wt\i2s-f8b2` 已退役。
> **下一步：派发 F8b3 参数面板**（依赖 F8a1 ✓、F8b2 ✓）。F8b3 须顺带：Desktop `CreateViewModel` 从 `IRecipeDraftStore.Save` 切到 `IRecipeDraftLineageStore.SaveVersion/AppendVersion` 并呈现类型化 trim/淘汰/supersede 结果（REQ-004 §7.5 第 6 条）；`PresentValidationFailure` 不再吞 `RecipeDraftStoreException`；简单模式审计建议②（存储失败测试改断言状态键+参数）。之后 F8b4 → 专业模式里程碑审计 → F8c → 收尾。
> **本会话运维经验（重要）**：①子 agent 会话可能在长命令/长会话中无声终止（转录停在 tool_use），**`resume` 在 Fable 端点不可用**（`Sand traffic is not supported`）——一律重派全新子 agent 并附完整任务书；因此任务书必须自足，开发子 agent 必须逐单元提交。②平台用量策略偶发误拦截只读审计（读普通 C# 文件时），应对：把审计拆成机械/语义两组小会话并行，单条 shell 命令 ≤3 分钟、长输出落 `%TEMP%` 日志。③主 agent 把 master 合入任务分支后**必须复跑构建**——git 无冲突不等于语义无冲突（F8b2 测试助手引用了 F8b1 已删常量）。④跑 `--no-build` 测试前确认 dll 时间戳晚于 HEAD，否则会被陈旧产物误导。
>
> **历史快照（2026-09-02 下午，崩溃恢复会话续）**：
> **治理定版（用户，2026-09-02）**：主 agent 只派发/调度/验收合并/维护状态板，**不亲自开发**；开发、测试、审计全部由子 agent 承担，子 agent 模型一律 **Fable 5 High**（`claude-fable-5-1-thinking-high`）。
> **F8b1 已关闭合入**：独立审计 PASS-with-remarks 零阻塞 → 微调子 agent 落实 3 条建议（`fdd550cc`）→ 合并 `d7f17d22`，合并态 Release 0/0、全量 **876/876**（AI.Tests 165）。REQ-004 三处旧名已勘误。worktree `D:\wt\i2s-f8b1` 待退役（本次会话末退役）。**当前基线：876 条 Release 0 失败。**
> **F8b2 开发中**：开发子 agent 在 `D:\wt\i2s-f8b2`（基于 `8428199d`，即 F8b1 合入之前的 master）。合并时注意：F8b1 已把 `src/VFXComposer.AI.Tests/Recipes/RecipeDraftStoreTests.cs` 等 5 个测试文件里的 `RecipePromptTemplate.Version` 机械改为 `RecipePromptAssembler.Version`；F8b2 被要求不改这些引用，但若其大幅改写 `RecipeDraftStoreTests.cs` 仍可能冲突，冲突解法一律"保留 F8b2 内容 + 把 `RecipePromptTemplate` 替换为 `RecipePromptAssembler`"。F8b2 派发时的九条定版见主计划 §7 F8b2 卡。交付后：主 agent 初审 → 合并 → 派 F8b3。
> 剩余序列：F8b2 → F8b3（参数面板）→ F8b4（精修回路）→ 专业模式里程碑审计 → F8c（按 ADR-008）→ 收尾。
>
> **历史快照（2026-09-02 上午，崩溃恢复会话）**：Cursor 在 F8b1/F8b2 并行开发期间崩溃并重装，对话记录全丢；本快照由重读仓库与 worktree 还原。
> **master `8428199d`（01:21）= origin/master，工作树干净**；最后一笔为简单模式里程碑审计登记（PASS-with-remarks，858/858）。§7 已闭：F8-0、R5、R6、F8a1、F8a2、简单模式审计。
> **F8b1（PromptAssembler 重构）——开发完成、未初审、未合并**：worktree `D:\wt\i2s-f8b1`，分支 `task/F8b1-prompt-assembler`，基于 `8428199d`，2 笔提交（`bf7b388a` 重构 02:07、`0ff5d401` 测试 02:10），工作树干净。改动 12 文件 +715/−218：新增 `RecipePromptAssembler`/`RecipePromptFragment`/`RecipePromptSection`，删除 `RecipePromptTemplate`；`AiContractVersions.RecipeSystemPrompt`→`RecipePromptAssembler = "vfxcomposer.ai.recipe-prompt-assembler/1"`，复合版本串 = 组装器修订 + 8 片段 `id/version`（写入 `PromptTemplateVersion`，store 未升版）；按片段边界拆分 16 KiB/条、`MaximumMessages`/256 KiB 请求界 fail-closed 为 `PayloadTooLarge`；`RecipeGenerationService` 与 5 个既有测试文件仅机械改引用；新增 `RecipePromptAssemblerTests` 16 条（哈希 pin 重构前消息序列、拆分正负向、版本串守卫）。**本会话复验：Release 构建 0/0，AI.Tests 163/163（147+16），全量 874/874（858+16）**。下一步：主 agent 初审（allow-list 应仅 `src/VFXComposer.AI.Contracts/**`、`src/VFXComposer.AI.Providers/Recipes/**`、`src/VFXComposer.AI.Tests/**`）→ 合并推送 → 状态板登记 → 退役 worktree。
> **F8b2（草稿 store 版本链）——仅建好 worktree，零提交、零 WIP**：`D:\wt\i2s-f8b2`，分支 `task/F8b2-draft-store-lineage`，01:22 与 F8b1 同时建立，feed 已复制，HEAD 仍在 `8428199d`。需重派或主 agent 自做（任务卡见主计划 §7；注意 RG-6 跨入口冲突行为定义必须可测试；lineage/origin 四值/parentDraftId/两级 cap/`UnsupportedVersion`/`Superseded`，store 升版仿 F3c 旧版 fail-closed）。
> 剩余序列不变：F8b1 合入 → F8b2 → F8b3（参数面板）→ F8b4（精修回路）→ 专业模式里程碑审计 → F8c（按 ADR-008）→ 收尾。

> **历史快照（2026-09-01 续接会话，§7 生成体验大改版进行中）**：
> **R6 与 F8a2 均已关闭合入推送**。R6：ADR-008（`60c39c02`）。F8a2：分支 `task/F8a2-simple-mode` 3 笔提交合并进 master，合并态复验 Release 0/0、全量 **858/858**（AI.Tests 147、Desktop.Tests 110）；交付细节见主计划 §7 F8a2 卡。worktree `D:\wt\i2s-f8a2` 已退役。
> **测试基线勘误（重要）**：此前登记的"Jobs.Tests 并行偶发 1 条 flake（`JobExecutorLockCrossProcessTests`，单跑必绿）"定性有误——真实原因是锁宿主工程 `src/VFXComposer.Jobs.Tests/JobExecutorLockHost/`（`ReferenceOutputAssembly=false` 引用）**不随 `dotnet build VFXComposer.sln -c Release` 构建**，宿主 dll 缺失即该测试必失败、存在即必绿。跑全量前先 `dotnet build src\VFXComposer.Jobs.Tests\JobExecutorLockHost\VFXComposer.Jobs.Tests.JobExecutorLockHost.csproj -c Release`。当前基线：全量 **858 条 Release 0 失败**。
> **下一步是简单模式里程碑审计（F8-0+F8a1+F8a2 合一次）**：若子 agent 派发可用则派只读独立审计（Opus 5 High）；不可用则主 agent 自行复核（allow-list、构建/测试复跑、任务卡验收标准逐条对照）后在状态板登记。之后按序 F8b1（PromptAssembler 重构）/F8b2（store 版本链，RG-6 冲突行为定义必须可测试）并行候选 → F8b3 → F8b4 → 专业模式审计 → F8c（按 ADR-008）→ 收尾（指针型 vfx-artist skill、DEV_MEMORY 终态）。
> F8a2 已知限制（报告归档）：①origin=preset 语义未落 store 字段（F8b2 升版时补，当前以 `promptTemplateVersion="preset/1"` 与 `correlationId="preset-<presetId>"` 可识别）；②构建诚实提示指向 manifest 批量路径，该路径不回写草稿状态（ADR-008 §1 事实 6 已记录，F8c 闭环后改指应用内构建）；③简单/专业模式切换 UI 与 ui-preferences `/2` 升版不在本卡（属 F8b4 卡）。

> **历史快照（2026-09-01，用户暂停外出）**：
> **R6 已关闭并推送**：`docs/rules/ADR-008_DESKTOP_BUILD_CLOSED_LOOP.md`（PROPOSED）由主 agent 直写，提交 `60c39c02` 含主计划状态板更新与 rules README 登记。定版要点：路线 B 独立构建宿主子进程（`apps/VFXComposer.BuildHost`，F8c 实现）、F3b 零 executor 裁决不重开、IL 扫描不豁免 Batch.Core（Desktop 侧仅新增单豁免类型 `BuildHostLauncher`）、锁探测留在宿主。**新确认断层**：draft-backed `BatchRecipeBuildPayload` 生产代码零调用点（唯一生产调用方 `BatchSubmissionService` 恒传 `draftId: null`），现状 manifest 构建路径不回写草稿状态——`ConfirmedAwaitingBuild` 是死状态，详见 ADR §1 事实 6 / §2.5。
> **F8a2 勘察完毕、未写代码**：worktree `D:\wt\i2s-f8a2`（分支 `task/F8a2-simple-mode`，基于 `226a2be5`）干净零提交。已勘察事实：①`CreateViewModel` 键+参数状态模式（`SetRecipeStatus(key, args)`），`ConfirmRecipeDraft` 走 `RecipeDrafts.Confirm(draftId, sha)`；②预置骨架落草稿走 `new RecipeDraftRecord(draftId, PendingConfirmation, ..., RecipeCanonicalJson.ComputeSha256(json), ...)` 公共构造（`draftId` 须过 `AiContractGuard.Identifier`：小写字母开头、[a-z0-9_-]、≤64；非 Failed 记录哈希必填），origin=preset 字段待 F8b2 store 升版，先落普通草稿并在报告注明；③快照 API 齐备：`RecipeTemplateCatalogSnapshot.Default` 的 `TemplateCatalogVersion`/`ContractRevision`/`BuildableArchetypes`/`BuildableDimensions`/`Templates`（能力提示行全部动态派生自此，REQ-004-04 禁硬编码）；④建议句/预警文案键：`RecipeSuggestionKeys` 闭集 17 键（值=键名，如 `RecipeSuggestionChooseCatalogTemplate`），Desktop catalog 须补双语文案；⑤catalog 纪律（会被既有测试强制）：`UiStringKeys` 常量值=自身名、双语键集全等、占位符集全等、中文必含汉字（`UiStringCatalogParityTests`）、**每个键必须被视图/VM 引用否则 fail**（`UiStringKeyWiringTests` 孤儿键测试）、XAML 禁硬编码文案（allowed literals 仅 ChatLlm/ImageGeneration）；⑥骨架合规参照 prompt 侧 `RecipePromptTemplate.ReferenceRecipeJson`（spark_projectile_2d 形状：三 stage 根齐全按序、≤2 模块、无 attachTo）,strict 预算常量 `RecipeCatalogPrevalidator.MaximumModules=2`；⑦骨架构建期测试要求过 L1+L1.5+F8-0 红线——`RecipeL1Validator` 是 AI.Providers internal（InternalsVisibleTo 仅 AI.Tests），骨架合规测试放 AI.Tests 或经公共 `RecipeCatalogPrevalidator.Prevalidate`/`RecipeGenerationService` 路径,Desktop.Tests 无 internals 可见性；⑧`AiDesktopRuntime.Unavailable` 的 `Save` 抛 `StorageFailed`——点卡在不可用运行时下走既有 `CreateRecipeStatusDraftStorageFailedWithCode` 分支即可；⑨`CreateView.axaml` 走 `Localization[Key]` 索引器绑定,`LocalizedViewBindingTests.IndexerBoundViews` 含 CreateView。
> 实现顺序建议：预置骨架资产（嵌入资源或常量，含每卡语言中立描述键）→ 卡片 VM+点卡落草稿 → 能力提示行 → 建议句可点击项 → 诚实提示两条（可复制命令+关编辑器）→ catalog 双语键 → Desktop.Tests+AI.Tests 骨架合规测试。诚实提示的可复制命令按现状指 manifest 工作流（`vfxc batch run`），其"不回写草稿状态"限制已在 ADR-008 记录,F8c 关闭后改指应用内构建。
> 测试基线：全量 **838 条 Release 0 失败**（Debug 下 Broker/LocalE2E 38 失败属既有设计）；Jobs.Tests 并行偶发 1 条 flake（`JobExecutorLockCrossProcessTests`，单跑必绿，已登记）。
> 剩余序列：F8a2 → 简单模式里程碑审计（F8-0+F8a1+F8a2）→ F8b1/F8b2（注意 RG-6）→ F8b3 → F8b4 → 专业模式里程碑审计 → F8c（按 ADR-008）→ 收尾。
> 本会话子 agent 派发工具不可用，主 agent 直接开发（worktree/提交/测试纪律照旧）。

> **历史快照（2026-08-31 深夜）**：
> 已关闭并推送：**F8-0**（prompt 合规样例+红线，合并 `2b60d885`，814/814）、**R5**（REQ-004 ACCEPTED + REQ-001 v0.5，`d7fb9bd3`；O-3 中文别名批准、O-4 origin 四值批准）、**F8a1**（L1.5 预校验层 VFXP 码表/建议键映射/上下界查询，合并 `226a2be5`，全量 **838/838**，AI.Tests 138）。
> **R6（ADR-008 Desktop 构建闭环设计）在途中断**：子 agent 转录停在 23:26 勘察阶段，ADR-008 未落盘，主仓干净——需**重派或主 agent 自写**（任务书要点见主计划 §7 R6 卡：三路线 A 进程内宿主/B 独立宿主子进程/C 维持现状,五个必裁问题含 IL 扫描策略）。
> **F8a2（Desktop 简单模式）worktree 已建好未开工**：`D:\wt\i2s-f8a2`,分支 `task/F8a2-simple-mode`,基于 `226a2be5`,feed 已复制。任务卡见主计划 §7（示例卡 4~6 张绑预置骨架、能力提示行、建议句渲染=RecipeSuggestionKeys→双语 catalog、确认后构建的诚实提示）。已勘察事实：`CreateViewModel` 已是键+参数模式,`RecipeDraftRecord.Create` 需要 `RecipeGenerationResult`——预置骨架直接落草稿可走 `new RecipeDraftRecord(...)` 构造(PendingConfirmation + `RecipeCanonicalJson.ComputeSha256` 算哈希,origin=preset 语义待 F8b2 store 升版,当前 store 无 origin 字段——F8a2 可先落普通草稿,报告里注明)。
> **本会话工具异常**：子 agent 派发工具(Task)中途不可用——若恢复则照旧派发;若仍不可用,由主 agent 直接开发(仍遵守 worktree/提交/测试纪律)。
> 剩余序列：F8a2 → 简单模式里程碑审计(F8-0+F8a1+F8a2) → F8b1(组装器重构)/F8b2(store 版本链,注意 RG-6 跨进程锁冲突行为定义) → F8b3(参数面板) → F8b4(精修回路) → 专业模式里程碑审计 → R6→F8c(构建闭环) → 收尾(指针型 vfx-artist skill、DEV_MEMORY 终态)。
> 测试基线:全量 **838 条 Release 0 失败**;Jobs.Tests 并行全量偶发 1 条 flake(`JobExecutorLockCrossProcessTests`,单跑必绿,既有缺陷已登记)。

> **更新（2026-08-31 晚）：追加需求「Desktop 中英双语系统」已完成关闭。** F7a（基建：184 键 catalog 的前 114 键、LocalizationService、`%LocalAppData%/VFXComposer/ui-preferences.json` 持久化、Settings 语言节）+ F7b（剩余六页全量迁移，catalog 达 184 键 × 双语，六页切换即刷）均合入 master 并推送；独立审计 PASS-with-remarks 零阻塞，7 条译文建议中 6 条已由主 agent 落实（Desktop.Tests 99/99 复验）。全量基线现为 **807 条 Release 0 失败**（台账勘误：F7a 前基线确为 747）。已知限制：Settings 页 `ProfileStatus`/`SecretPresence`/`Chat、ImageBindingStatus` Unavailable 回退值为渲染快照，切语言后下次状态更新才刷新。设计与验收记录见主计划 §6。
>
> **终态（2026-08-31）：优化计划全部关闭，无在途任务、无待办。** master `613dab47` 与 `origin/master` 同步、工作区干净；主计划 18 项 + 延后清算四项 D1–D4 全部合并推送（D1–D4 合并提交 `613dab47`，独立审计 PASS）。`D:\wt\` 已清空（`i2s-f6` 于本日退役），已合并任务分支 `task/F6-e2e-acceptance`/`task/O2-acceptance-script`/`task/O3-baseline` 删除；`codex/m1-protocol-unity`、`codex/m2-production-read` 各 1 笔未合并提交按既定裁决**归档保留**。下方历史条目中的"待办/待批准合并"均已完成，仅作过程记录。若要继续开发，从主计划"后续债务"与 `PROJECT_UNDERSTANDING.md` 起读即可。
>
> 暂停时间：2026-08-29 18:20 前后。恢复开发时按 §3 步骤执行即可无缝续接。
> **更新（2026-08-30，续接会话）**：F6 已合并 master `f38a9563` 并推送 GitHub，优化主计划 18 项全闭环。随后用户「都做了」裁定，**延后清算四项在分支 `task/deferred-cleanup` 全部交付并逐项验证**：D1 RestoreProvenance 原子回滚 `5565149d`（EditMode 28/28）｜D2 Jobs 页批次折叠 `23de31dd`（Desktop 39/39）｜D3 执行器锁跨进程真杀 `365857b5`（Jobs 57/57）｜D4 dependencyHash 移出入库清单 `9c5e5ace`（EditMode 全量 686/686，229 清单迁移）；台账 `docs/stage-notes/F6_DISCRETIONARY_DISPOSITION.md`。分支尖端复验：**EditMode 686/0/54、.NET Release 全 11 工程 0 失败（合计 747）**。**待办：用户批准后合并 `task/deferred-cleanup`→master 并推送**（分支未推、未合、未审计后改动）。
> **更新（2026-08-30，崩溃恢复会话）**：Cursor 曾在 F6 开发中途崩溃。核查结论：git 完好、master 干净且与 origin 同步。环境验证通过——Release 0/0、.NET 全量 **733/733**（与基线逐位吻合，SDK 8.0.420）。分支盘点：所有 `codex/u*`/`codex/a*`/`usermode-integration` 相对 master 0 未合并（早经 P0-1 并入）；`codex/m1`/`codex/m2` 各 1 未合并（归档保留）。**关键恢复**：F2 已合入解锁 F6，且 F6 子 agent 崩溃前的未提交 WIP 从 worktree `D:\wt\i2s-f6` 找回，补配套断言后转绿并提交至分支 `task/F6-e2e-acceptance`：**F6 已收尾（审计 PASS，待用户批准合并）**：分支 `task/F6-e2e-acceptance` 7 笔提交（`a23c6b6c`→`1579fe43`）。必做①-⑥ 全落地；流程一真机 Unity E2E 通过（Editor `E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor`，对主仓 `project/` 构建 spark_projectile_2d，三件套零越界，还原纪律清理）；流程二 E2E（真实 CliRunner + MCP 冒烟 + `eng/run-f6-e2e-acceptance.ps1`）；裁量项全处置（仅 F3-3 跨进程锁真杀测试记因不做）。独立审计 PASS 零阻塞：Release 0/0、全量 **745/745**、allow-list 全合规、`project/**` 禁区零命中。证据 `docs/stage-notes/F6_E2E_EVIDENCE.md`、`F6_DISCRETIONARY_DISPOSITION.md`。**待办仅一步**：用户说「推送」后，主 agent 合并 `task/F6-e2e-acceptance`→master 并推 GitHub，即收尾整个优化计划。用户定：暂不推送、最终视觉由用户在 `spike/image_to_smart`（不含构建包，纯视觉工程）统一签署。F6 分支未推、未合、未审计后改动。
> **更新（2026-08-29 20:00）**：O3、F1、F3、F3b 均已合入 master（最新 `e36d5a8d`，全量 538/538）。REQ-003-12 裁决为条件豁免（F2 维持 batchmode 即有效）。当前在途：O4（Unity 测试 triage）、F4（CLI 批量入口）。下一步：O4 归零后拍板 F2 生产闸（§2 决策 2）并派发 F2；F4 交付后审计合并，再定版 F5 MCP 底座（§2 决策 3，倾向手写 stdio JSON-RPC）。另见主计划"运维事件"：worktree 退役操作仅由主 agent 在确认无在途任务时执行。
> 文件夹导航：`OPTIMIZATION_MASTER_PLAN.md`（总计划+任务卡+状态板）｜`CODING_STANDARDS.md`（验收标准）｜`PROJECT_UNDERSTANDING.md`（项目理解）｜`SESSION_LOG_2026-08-29.md`（本次对话记录）｜`WORKTREE_RETIREMENT.md`（worktree 退役清单）｜`BASELINE_REPORT.md`（O3 交付后出现）。

## 1. 暂停时刻的精确状态

**master = `fa069166`，已推送 GitHub（snowpeak008/image_to_effect），工作区除下述待提交项外干净。**

已关闭：P0-1、P0-2（含 2b）、O1、O2、R1（v0.3 微调在途）、R2、R3、R4（v1.1 微调在途）。

### 在途任务（暂停时仍在后台运行的子 agent）

| 任务 | 内容 | 完成后处置 |
|---|---|---|
| ~~ADR-007 v1.1 微调~~ | 已完成并转 ACCEPTED，已合入 | 完成 |
| ~~REQ-001 v0.3~~ | 已完成，已合入 | 完成 |
| O3 基线 — **已交付待验收**（分支 `task/O3-baseline`，提交 `a6ee7253` 锁修复 + `f73ccea0` 基线报告，未合并） | 锁文件修复（3 文件、无版本变化）、锁定 restore 18/18 通过、构建 0/0、.NET 测试 450/450（独立复核与 P0-1 一致）、Unity EditMode 596 通过/8 失败/53 跳过 | 用户回来后：验收合并 → 停用轻闸 `-SkipLockedRestore`。**新发现：Unity 包 8 个确定性既有测试失败**（契约 pin 漂移 ×2、错误码清单不同步、preview 场景缺 driver ×2、W24FS107≠109、句柄暴露、状态注册断言；两种图形模式一致，与 O3 改动无关）——**F2 以 Unity 编译链路为验收面前必须先 triage 归零或明确豁免**，建议作为新任务 O4 派发 |
| F1 开发 — **已中断暂停**（分支 `task/F1-recipe-generation`，WIP 提交 `558cfcb1`，11 文件 +2589 行，未合并） | 已完成并提交：契约层 DTO（含草稿状态机与 `IRecipeGenerationChannel`）、`IAiDesktopRuntime` 扩展、模板目录快照（嵌入资源）、规范化哈希、输出解析器、L1 校验器、prompt 模板。半成品：`RecipeGenerationService`（重试编排，设计定稿未写文件）。未开始：`RecipeDraftStore`、运行时接线、Desktop Create 页、全部测试 | 恢复时 resume 原 F1 agent（id 见 SESSION_LOG）或重派：从 `RecipeGenerationService.cs` 续写 → `RecipeDraftStore` → 接线 → Desktop 页 → 测试；**先跑 `dotnet build VFXComposer.sln -c Release`**（WIP 新文件未经编译验证）；注意 Desktop.Tests 的 `FakeDesktopRuntime` 等实现需补 `IAiDesktopRuntime` 新增的两个成员 |

### 工作区待提交文件

无——小任务批（ADR-007 v1.1 / REQ-001 v0.3 / rules README / CODING_STANDARDS §3.2 更新）已全部合入推送；剩余在途仅 O3 与 F1 两个 worktree 任务，交付物在各自分支上，等用户回来后验收合并。

## 2. 待主 agent / 用户拍板的决策（按优先级）

1. **F3 执行器宿主形态**（派发 F3 前定版）：倾向方案——执行层做成库 + 跨进程单写者 durable lock（复用 `ProviderConfigurationRevisionLock` 模式），Desktop/CLI/MCP 三入口各自在进程内宿主执行器，锁保证全局单并发；避免常驻服务，符合 fail-closed 与"无新网络面"。备选：独立宿主进程（利于 `--detach`，但引入进程管理复杂度）。
2. **F2 生产闸**（派发 F2 前定版）：走既有 `BuildProduction`（含 `W24S5ProductionGateRequest` contract-first 准入）还是新增等价受限入口。
3. **F5 MCP 实现底座**：官方 MCP C# SDK 不在批准 feed → 要么走 feed 准入流程，要么手写 stdio JSON-RPC（REQ-002 §7 工具面是闭集，手写量可控，倾向手写）。
4. **batchmode 冷启动延迟**（用户拍板）：分钟级冷启动若不可接受，需另立 ADR 引入常驻可写 Unity 会话；当前按短生命进程走。
5. **旧分支处置**：`codex/m1-protocol-unity`（worktree 有未提交 Unity Worker 生产连接器 WIP）、`codex/m2-production-read`（约 1900 行生产模式 Broker 监督代码）——倾向归档保留不收编；14 个已并入 worktree/分支等 O2/O3/F1 的 worktree 用完后按 `WORKTREE_RETIREMENT.md` 统一退役。

## 3. 恢复开发步骤

1. 查看在途四个子 agent 的完成通知/交付报告（若会话已断，直接看各 worktree 分支的提交与工作区 diff：`git -C D:\wt\i2s-o3 log --oneline master..HEAD`，`git -C D:\wt\i2s-f1 log --oneline master..HEAD`，主工作区 `git status`）。
2. 验收并提交小任务批：ADR-007 v1.1（确认状态 ACCEPTED）+ REQ-001 v0.3 + `docs/rules/README.md`，一个 docs 提交推送。随后把 `CODING_STANDARDS.md` §3.2 的"裁决前只读"措辞更新为引用 ADR-007 定版规则（主 agent 自己改，属管理文件）。
3. 验收 O3：核对锁文件 diff 与基线数字 → 合并 `task/O3-baseline` → 验证 `dotnet restore -p:RestoreLockedMode=true` 全绿 → 推送 → 通告轻闸停用 `-SkipLockedRestore`。
4. ~~验收 F1~~：已完成——独立审计 PASS（锁定 restore 18/18、构建 0/0、全量 483/483），合并 `fd7b508f` 推送；3 条非阻塞建议登记在主计划状态板下方。
5. 按 §2 拍板决策 1，派发 F3（依赖 R3✓、P0-1✓）；F1 合并后派发 F2（依赖 F1✓、R4✓，注意 F2 任务卡里的生产闸决策与 Windows 保留名负向测试要求）。
6. 之后按主计划 DAG：F4（依赖 F1/F3/R2）→ F5 → F6。每步遵循 §5 治理模型（见 `PROJECT_UNDERSTANDING.md`）。

## 4. 派发子 agent 的模板要点（沿用本次会话有效实践）

- **模型选择（用户定版，2026-08-29）**：后续子 agent 一律用 Claude Opus 5 Thinking (High)，仅限 Claude 系；轻量文档修订由主 agent 直接做，不派子 agent。
- 任务书必含：先读主计划任务卡 + CODING_STANDARDS；精确 allow-list；worktree 指令（代码任务）+ **复制批准 feed**（`.codex_tmp/w24-phase1-approved-feed` 未跟踪，新 worktree 没有它 restore 必失败）；PowerShell 5.1 语法警告；禁止 push/合并/越界；交付报告格式（改动清单/命令与数字/已知限制）。
- 文档任务在主工作区只新增文件、禁 git 写操作，由主 agent 验收后提交。
- 交付后：主 agent 初审（小任务）或派只读独立审计（PRD/ADR/代码），建议级问题 resume 原作者一轮微调，微调后由主 agent 提交推送并更新状态板。
- 已知踩坑：并行任务的工作区 diff 会互相混入 `git status`，验收时只 add 本任务 allow-list 文件；构建副作用会改写漂移的锁文件（O3 修复合并前），非本任务文件用 `git checkout` 还原。
