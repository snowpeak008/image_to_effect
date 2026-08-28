# W11/W12/W14/W17 独立内容源码与机器验收报告

> 日期：2026-08-25  
> 状态：源码与机器门禁通过；W11/W12/W14/W17 用户视觉结论均为**拒绝（批量签署）**  
> 范围：W11 环境天气、W12 打击反馈、W14 屏幕反馈、W17 游戏交互 UI

## 1. 交付结果

- 30 份合法 Recipe、30 条稳定 `set_content_param` Patch、30 个 strict Runtime Entry。
- 4 个正式 Preview Scene：Environment 7、HitFeedback 7、ScreenUI 6、GameUI 10。
- 正式 UI 条目使用 Canvas/Graphic/RectMask2D，零 ParticleSystem；世界条目使用共享 Mesh/Material 与固定容量粒子。
- 通用 `IVfxRuntimeEntry` 生命周期外，提供 intensity、wind、stack、rarity、fill、anchor、world endpoints、external MPB 与 skip-to-reveal 协议。
- `lifesteal_link_beam_2d` 由真实 LineRenderer 按 source/target 和 sag 采样；`reward_fly_collect_ui` 沿端点贝塞尔弧运动；`parry_spark_impact_3d` 启用真实粒子世界碰撞与反弹。

## 2. 构建、所有权与预算

独立编译器版本为 `planned-independent-2`。每个 Manifest 的 `enforcement=strict`，`ownedOutputs[]` 仅拥有本条目的 Prefab，共享风格资源进入 `dependencies[]`；本批无成品独占 PNG，`localTextureBytes=0`。第二次构建必须为 `Unchanged`。

UI 普通条目元素数不超过 24，gacha 条目不超过 48；Environment 固定三层 ParticleSystem，总容量不超过 160。Preview driver 被项目规则禁止进入任何生产 Runtime Entry。

## 3. 机器证据

- Compile：exit 0。
- 定向 EditMode：5/5，`test-results/w11-w17-independent-edit-v4.xml`。
- 定向 PlayMode：5/5，`test-results/w11-w17-independent-play-v2.xml`。
- 全量 EditMode：211 total / 176 passed / 0 failed / 35 historical Explicit skipped，`test-results/w11-w17-full-edit-v2.xml`。
- 全量 PlayMode：35 total / 29 passed / 0 failed / 6 Explicit skipped，`test-results/w11-w17-full-play.xml`。

覆盖内容包括 30 个内容注册与非法值、幂等构建、Canvas/World 语义、所有权与预算、30 条 Patch 校验及一次真实 Patch 事务、4 个 Preview 隔离、持续循环与池化、外部 MPB 不换材质、端点束几何、锚点/稀有度/跳过/填充协议和错误码。

## 4. 用户视觉签署（2026-08-25）

用户原话：

> 拒绝，无法商用，后续的特效类，都不验收了，这是通病了，都是同样的拒绝，无法商用，

用户明确决定不再逐 Scene 检查后续特效类，并将 W11 环境/天气、W12 打击/连携、W14 Screen/UI、W17 游戏交互 UI 的当前候选统一签署为**拒绝**。四个 Scene 未作逐条/逐帧视觉核对；本记录不把批量决定伪造成用户已观察每个条目的天气层次、打击力度、安全区或 UI 动态问题。

“无法商用”只记录为用户对视觉制作完成度的评价，不作版权、许可或法律解释。四个当前候选均记为 `rejected`；机器门禁仍只证明协议、构建、预算与生命周期。本次签署未授权重做、修改源码/资产或生成下一候选。
