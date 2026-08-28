# W9/W10/W16 风格专项源码与机器验收报告

> 日期：2026-08-25
> 状态：源码与定向机器门禁通过；W9/W10/W16 用户视觉结论均为**拒绝（批量签署）**

## 交付

- W9：9 个 2D 专项 + 1 个 fireball pixel 变体。
- W10：9 个 3D 专项 + 1 个 prismatic shield holo 变体。
- W16：6 个新风格 token、6 个新内容、6 个既有内容变体。
- 合计 32 个 strict Recipe/Runtime Entry/Patch，3 个正式 Preview Scene。
- 风格总表由 8 扩为 14 token；Parser 与 Validator 支持各管线专属参数。
- 共享资源新增 Facet ×3、Gear ×3、Anime Smear Atlas、Symbol Atlas、Nebula ×2、Star Atlas；全部作为 dependency，源 PNG 合计低于 100 KB。

## 机器证据

- Compile：exit 0。
- 定向 EditMode：6/6，`test-results/w9-w10-w16-edit-v1.xml`。
- 定向 PlayMode：3/3，`test-results/w9-w10-w16-play-v1.xml`。
- 全量 EditMode：217 total / 182 passed / 0 failed / 35 historical Explicit skipped，`test-results/w9-w10-w16-full-edit.xml`。
- 全量 PlayMode：38 total / 32 passed / 0 failed / 6 historical Explicit skipped，`test-results/w9-w10-w16-full-play.xml`。

验证包括 32 个 Recipe、14 token、类型/范围拒绝、32 个幂等 strict Prefab、32 条 Patch、共享所有权与源体积、原 default Recipe/Manifest 不变、三个 Preview，以及像素/手绘时间量化、全息确定性故障和持续幽魂/半写实池化。

## 用户视觉签署（2026-08-25）

用户原话：

> 拒绝，无法商用，后续的特效类，都不验收了，这是通病了，都是同样的拒绝，无法商用，

用户明确决定不再逐 Scene 检查后续特效类，并将 W9 2D 风格专项、W10 3D 风格专项、W16 风格包二的当前候选统一签署为**拒绝**。三个 Scene 未作逐条/逐帧视觉核对；本记录不伪造像素帧感、手绘、水墨、半写实、全息、暗黑或第二风格包的单项观察结论。

“无法商用”只记录为用户对视觉制作完成度的评价，不作版权、许可或法律解释。三个当前候选均记为 `rejected`；机器门禁不等于视觉通过。本次签署未授权重做、修改源码/资产或生成下一候选。

## 后续独立 next-candidate

用户此后已通过 W24 全项目开发授权允许继续实现。本报告上方的旧交付、旧 Scene、旧 XML 机器证据和拒绝原文不改写；新的 32 项候选使用独立 compiler、独立 suffixed id、独立 Preview Scene 与新源码目录。隔离 shadow 已真实生成 32 份 Recipe/Prefab/Manifest 与 3 份 Preview Scene，通过定向 EditMode `4/4`、Preview `1/1`、PlayMode `4/4` 及 16 份共享回归 `67/67`，定向加回归合计 `76/76`；独立终审为 **GO，P0=0、P1=0**。审计同时确认 67 份 production `.meta` GUID 唯一且没有旧 GUID 引用，三份 Preview 的 `LegacyRuntime` header/labels 完整。

这些新结果只证明 next-candidate 的机器门禁，不替换上方旧候选证据，也不撤销用户对旧候选的拒绝。新生成物尚未从隔离 shadow 晋升 canonical，且机器 GO 不是视觉签署；当前状态继续为 `NEXT_CANDIDATE_VISUAL_PENDING`。源码、预算、当前 XML 边界、复现入口与精确同步清单见 `docs/stage-notes/W9_W10_W16_NEXT_CANDIDATE_REPORT.md`。
