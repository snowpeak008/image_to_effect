---
name: vfx-artist
description: Pointer skill for AI-assisted VFX authoring in this repository — generate, hand-tune, refine, and build Recipe v1 effects through the guided-generation pipeline; use when creating or revising effects via Desktop/CLI/MCP, not for Unity asset editing or design contracts.
---

# VFX Artist（指针型 skill）

本 skill 不含独立知识，只指向仓库内的权威资产。生成/精修/构建一个特效时按下列指针取材；与本文冲突时以被指向的文件为准。

## 表达空间（先读，能力边界）

- 模板目录快照：`src/VFXComposer.AI.Providers/Recipes/Assets/`（经 `RecipeTemplateCatalogSnapshot.Default` 消费）。当前 6 模板 / 11 参数 / 2D 火系弹道（projectile, 2d），strict 预算：全 recipe ≤2 渲染模块、禁 attachTo、三 stage 根（launch/travel/impact）固定齐全。
- 能力提示与数字一律从快照动态派生（REQ-004-04），不得手写。

## 生成链路（Recipe v1）

- 需求权威：`docs/requirements/REQ-001_CHAT_TO_VFX.md`（单轮生成）+ `docs/requirements/REQ-004_GUIDED_GENERATION.md`（双模式/版本链/精修/参数面板，57 条编号需求 + 20 AC）。
- prompt 组装：`src/VFXComposer.AI.Providers/Recipes/RecipePromptAssembler.cs`（片段化 + 复合版本串；合规参考样例 spark_projectile_2d 形状）。
- 校验层级：L1 `RecipeL1Validator`（结构，Error 即拒）→ L1.5 `RecipeCatalogPrevalidator`（目录感知预警，VFXP 码，不阻断）→ Unity L2（构建期权威）。
- 预置骨架（零 AI）：`RecipePresetSkeletons`（6 卡，origin=preset）。
- 手改：`RecipeParameterEditor`（VFXE 7 码，不 clamp，落 human_edit 新版本）。
- 精修：`IRecipeRefinementChannel`（1+N 预算、单 route、三件套上下文）；艺术家知识唯一真源 `docs/ai-workflow/refine-artist-knowledge.md` → 入库 fragment `Assets/refine-artist-knowledge.fragment.json`（改源文档必须重导出并 bump version，SHA-256 pin 测试强制）；覆盖守卫 `RecipeRefineOverrideGuard`（三条件还原，别名词表同源）。
- 版本链：`RecipeDraftStore`（formatVersion 2，lineage/origin 四值/Superseded/两级 cap/跨进程锁），语义见 REQ-004 §7/§8。

## 构建链路

- 设计权威：`docs/rules/ADR-007_CONTROLLED_PROJECT_MUTATION.md`（写入面三成员闭集）+ `docs/rules/ADR-008_DESKTOP_BUILD_CLOSED_LOOP.md`（Desktop 闭环）。
- Desktop 应用内构建：确认草稿 → Create 页"构建"→ `apps/VFXComposer.BuildHost` 宿主自验入队执行 → MarkBuilt/MarkBuildFailed 回写 →"刷新状态"可见。构建前关闭 Unity 编辑器（`WaitingProjectLock` 可取消）。
- 批量：`vfxc batch run <manifest>`（清单 schema 与样例在 `batches/`）；MCP 工具面见 `docs/requirements/REQ-002_BATCH_CLI_MCP.md`。

## 相邻 skill 与纪律

- 设计合同（实现前的视觉设计定版）：`docs/skills/unity-vfx-design-director/SKILL.md`。
- 视觉 QA：`docs/skills/unity-vfx-visual-qa/`。
- 开发纪律：`docs/plans/CODING_STANDARDS.md`；计划与状态板：`docs/plans/OPTIMIZATION_MASTER_PLAN.md`；恢复指南：`docs/plans/DEV_MEMORY.md`。
- 扩充表达空间（新模板/元素族/原型）属 Unity 侧创作工程（模板库扩充 track），不是改 prompt 能解决的——先读主计划 §7 暂缓项。
