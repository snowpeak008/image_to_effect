# W1 风格底座与 W2 VFX Studio 实施报告

状态：**源码与机器门禁完成；W1 风格底座已被用户视觉拒绝；W2 Studio UI 等待最终用户签署**  
日期：2026-08-24  
权限边界：本报告不代替用户对风格差异、界面可用性和最终画面的视觉签署。

## 1. W1 已实现

- Recipe `style` 契约接入 8 个正式 token：`stylized/cartoon/pixel/inkwash/semireal/holo/dark/neon`，每项声明 2D/3D 支持和稳定材质参数。
- 新增共享 Shader：`VfxLayeredRamp` 主实现及 SoftNoise、HoloFresnel、PixelQuantize、InkBrush、DissolveEdge 入口；全部位于正式 Shared 目录。
- 确定性生成共享资源：6 个 Mesh、14 个 64×64 Mask、3 个 16×1 LUT；共享资源作为 Manifest `dependencies[]`，不复制到单个 Generated 目录。
- 新增通用 Player-safe `StyledVfxController`，覆盖 one-shot、sustained、event-driven 的 Play/Stop/Reset 和空闲不可见契约。
- 六份打样 Recipe 已构建为 strict Runtime Entry：三份纯风格样例 `fireball_2d_cartoon`、`fireball_2d_neon`、`frost_impact_2d_dark`，以及三份计划内“能力+皮肤”成品示范 `cap_demo_fan_wave_cartoon_2d`、`cap_demo_charge_occlude_holo_3d`、`cap_demo_telegraph_nova_holy_3d`。与旧 `fireball_2d` 组成七格预览场景 `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples.unity`。
- `frost_impact_2d.to-dark.patch.json` 使用裸数组和正式 `set_style_token/set_palette` 操作；真实 Patch 校验与 revision 流程通过。
- 新增稳定错误码 E1800–E1803，并与 `docs/release/ERROR_CODES.md` 双向审计。

### 1.1 W1 最终用户视觉签署（2026-08-25）

用户对 `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples.unity` 的结论为**拒绝**。用户原话：“拒绝；W1样例尺寸未统一，Stylized基准过大、Dark frost过弱，Holy新星与Holo射线跨格、裁切并遮挡标签；场景缺少格内约束和8种style token的完整视觉对比，整体未达到商用级视觉完成度。”

| 原计划判断点 | 当前画面与技术自检 |
|---|---|
| 同 Archetype 风格样例可在统一尺度下直接比较 | `Stylized baseline` 体量远大于 Cartoon/Neon，`Dark frost` 又明显过弱；样例没有统一视觉包围盒或屏占比 |
| 七格互不遮挡、标签完整 | Holy 新星覆盖中下部多个格子和文字；Holo 射线延伸出右侧；其他能力轨迹也跨格，标签直接压在特效上 |
| 8 种首批 style token 均有可见差异证据 | Scene 只有 Stylized/Cartoon/Neon/Dark 四类直接样例及三份组合示范；没有 Pixel/Inkwash/Semireal 的独立对比格，无法完成 8 token 同场视觉验收 |
| 三份“能力+皮肤”组合清楚可读 | 组合之间的原始尺寸和轨迹范围未归一，Holy/Holo 等覆盖邻格，使能力与风格归属难以辨认 |
| 达到可继续作为商用级制作底座的视觉完成度 | 用户明确未认可；该结论只指当前视觉质量，不解释任何版权、许可或法律状态 |

直接技术原因：Preview 构建器把各 Prefab 以原始 `localScale=1` 直接放入 `2.4 × 1.5` 的紧凑格位，没有按 Renderer Bounds 归一化、格内缩放、独立 viewport、裁剪遮罩或背景板；七个 Entry 又由同一 Driver 在每轮同时播放。相机 `orthographicSize=3.05`，标签固定在每格局部 `y=-0.83`，因此大体量径向/射线效果会跨格、裁切并遮挡文字。

用户提供的是当前任务中的 Game View 单帧截图；它足以证明跨样例尺寸、遮挡和裁切问题，但不单独扩写为“单个效果随时间抖动变尺”的逐帧结论。当前 W1 候选状态为 `rejected`；用户未授权重做、修改源码/资产或生成下一候选。

### 1.2 W1 后续独立 next-candidate（2026-08-25）

上述拒绝归档后，用户在后续任务中另行授权 W1 重做。本轮保持旧 Scene、旧 Generated 输出与拒绝原话不变，新增独立 compiler `w1-style-next-candidate-1`、11 份 `StyleGallery` 冻结 descriptor、专用 Runtime/Preview driver，以及独立目标 Scene `Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity`。新状态为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`，不继承旧候选结论。

结构修复不依赖标签或单一参数：八个 token 分别绑定真实 Shared Material mode、三层 Mesh 组合与不同 timing profile；三份“能力 + 皮肤”由 CapabilitySampleTrace 驱动真实 Shard、LineRenderer、Ring/Burst 载体。所有条目 root scale 为 1，声明同一 `1.56 × 1.08 × 0.50` local envelope；11 个效果格以独立 layer 和 Camera viewport 硬裁剪，Overlay 标签 safe band 与所有效果 viewport 分离。固定上限为 10 GameObject、6 Renderer、0 ParticleSystem、每 Entry 1 个 shared Material，并提供 bounds、预算、clean gap、复播和实际行为观测点。

源码阶段 Runtime、Editor、EditMode+Preview、PlayMode 四组 Roslyn 静态编译均 exit 0。随后隔离 Unity 已真实生成 11/11 Prefab、11/11 Manifest 与新 Preview Scene，并通过 W1 EditMode `3/3`、Preview `1/1`、PlayMode `4/4`；W-C1/W-C2/W-C3、旧 W1 与 W3+ 的定向共享回归也全部通过。机器门禁不产生用户视觉签署，新候选仍为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`。完整实现、修复记录与复现入口见 `docs/stage-notes/W1_NEXT_CANDIDATE_REPORT.md`。

## 2. W2 已实现

- `Tools/VFX Composer/Studio` 提供 Library、Create、Preview、Patch、Review 五页签。
- Library 直接扫描 Recipe 与 Generated Manifest，支持按 Archetype、维度、元素、风格和能力 token 过滤；未创建平行索引资产。
- Create 生成完整草稿并保留 `behavior`；任何保存/构建都先显示路径和确认，不静默写入。
- Preview 只打开保存的正式 Preview Scene；明确标注它是操作预览，不自动生成视觉通过结论。
- Patch 队列确定性序列化，调用既有 Validator 与事务式 Patch 服务；支持导出和确认后 Apply。
- Review 自动项与人工项分离。人工项默认未选，只有五项全选、reviewer 非空且再次确认时才写 `REVIEW.md`，因此本轮不会提前代签用户验收。

## 3. 机器验证

| 门禁 | 结果 | 证据 |
|---|---:|---|
| Compile | exit 0 | `test-results/unity-compile.log` |
| W1/W2 EditMode 定向 | 5/5 pass | `test-results/w1-w2-style-studio-v2.xml` |
| Styled Runtime PlayMode 定向 | 2/2 pass | `test-results/w1-w2-styled-runtime.xml` |
| 全量 EditMode | 198 total / 163 pass / 0 fail / 35 historical Explicit skipped | `test-results/w1-w2-full-editmode.xml` |
| 全量 PlayMode | 24 total / 18 pass / 0 fail / 6 graphics evidence Explicit skipped | `test-results/w1-w2-full-playmode-v2.xml` |

计划收尾补测（2026-08-24）：新增三份“能力+皮肤”示范后，W1/W2 定向 EditMode `6/6`、示范真实 BehaviorTrace PlayMode `1/1`；最终全量统计更新为 EditMode `224 total / 189 passed / 0 failed / 35 skipped`、PlayMode `44 total / 38 passed / 0 failed / 6 skipped`。

首次全量 PlayMode 自动执行了一条旧图形证据录制器，并在无头 URP 粒子绘制时触发 Unity 原生崩溃、未产生 XML。该结果作废；五个 `*VisualCaptureTests` 已按职责统一标为 `Explicit`，普通回归只运行产品生命周期测试，显式录图入口仍保留。整改后全量 PlayMode 通过。该经验已登记为 EXP-029。

## 4. 最终验收队列

最终验收状态：W1 已于 2026-08-25 被用户视觉拒绝；W2 仍等待用户检查。原验收队列为：

1. 七格样例是否能一眼区分 stylized/cartoon/neon/dark，并看清 fan+wave、charge+occlude、telegraph+nova 三个能力组合；
2. Studio 五页签的信息密度、操作路径与确认提示是否符合使用习惯；
3. Library 的按能力浏览、Preview、Patch 和 Review 是否满足实际制作流程。

W1 旧候选的拒绝保持有效；其后另行授权的新候选已完成隔离机器门禁，但不得据此改写为视觉通过，仍为 `W1_NEXT_CANDIDATE_VISUAL_PENDING`。W2 在用户签署前仍只标记“源码与机器门禁完成”，不标记“界面视觉/可用性验收完成”。
