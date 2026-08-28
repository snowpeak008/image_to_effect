# W17 游戏内交互 UI 特效包（10 个特效）

> 实现状态（2026-08-25）：10/10 Canvas 语义 Runtime Entry、Recipe、语义 Patch与交互协议已完成；稀有度、跳过、填充、锚点及奖励端点运动均有机器断言；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：面向**玩家操作反馈**的 UI 特效层——按钮、卡牌、宝箱、抽卡、奖励飞行、签到等。这与 W14（战斗屏幕反馈）和 W2（编辑器工具 UI）是三个不同的层；本包全部运行在 Canvas 语义下，遵守 Screen/UI Archetype 协议，并新增"UI 交互挂点"协议：特效锚定到外部传入的 RectTransform，跟随其缩放/位移。
> 批次预览场景：`VFXPREVIEW_GameUI.unity`（含模拟界面：按钮组/卡牌/宝箱/奖励栏，模拟器不进正式 Prefab）。

## 1. 清单

| id | 生命周期 | 一句话 |
|---|---|---|
| button_press_fx_ui | event | 按钮按压：内凹波纹+边缘光扫+2 颗星屑 |
| button_confirm_burst_ui | event | 重要确认：环爆+放射线+按钮描边流光一周 |
| card_flip_reveal_ui | event | 卡牌翻转揭示：翻面闪光+稀有度决定的边缘喷发 |
| card_merge_fx_ui | event | 卡牌合成：两卡吸附对撞+升阶光柱 |
| chest_open_burst_ui | event | 宝箱开启：盖缝漏光渐强+爆开金光与奖励预闪 |
| gacha_single_reveal_ui | event | 单抽演出：光球坠落→裂纹→按稀有度分级爆发 |
| gacha_ten_sequence_ui | event | 十连演出：流星队列+最高稀有度压轴强调 |
| reward_fly_collect_ui | event | 奖励飞行：金币/道具弧线飞入栏位+到账脉冲 |
| daily_check_stamp_ui | event | 签到盖章：印章砸落+墨圈+完成勾光 |
| progress_charge_fx_ui | sustained | 进度条充能：条内流光+满档溢出星光提示 |

## 2. 规格卡要点

- **button_press / button_confirm**：极轻量（元素 ≤8）；按压波纹以按钮 Rect 为遮罩内凹，不外溢遮挡相邻控件；confirm 版描边流光沿 Rect 圆角路径一周（路径由 Rect 自动求得，任意尺寸按钮通用——通用性是验收项）。参数：palette、scale_with_button。
- **card_flip_reveal**：翻转本体由外部 UI 动画驱动，特效只做翻面瞬间闪光帧+背面辉光渐强+揭示后按 `rarity 1–5` 分级喷发（复用 W15 loot 稀有度色表，保持全项目稀有度语言统一）。参数：rarity、flash_scale。
- **card_merge**：两源卡位置外部传入，吸附拖尾对撞→白闪→升阶光柱从结果卡底升起+新边框流光点亮。参数：source_count 2–3、result_rarity。
- **chest_open_burst**：三段——盖缝漏光呼吸（可循环等待玩家点击）→ 开启爆金光+尘埃 → 奖励预闪光点 3–5 颗弹出（衔接 reward_fly）。参数：leak_intensity、burst_scale、tease_count。
- **gacha_single_reveal**：光球坠落弹跳 2 次→表面裂纹逐条亮→爆发。稀有度分级表：1–2 档蓝白小爆；3 档金环双圈；4 档紫金柱+放射线；5 档全屏金瀑+慢闪白（全屏部分仍守 W14 中心可读性红线的豁免档：演出类允许 ≤0.8s 全屏，Manifest 声明 `fullscreen_grace`）。参数：rarity、buildup_time（可被跳过按钮截断——`skip_to_reveal` 运行时方法，交互协议验收项）。
- **gacha_ten_sequence**：十颗流星依次落位成 2×5 阵→逐个小揭示→最高稀有度卡位压轴单独走 single_reveal 强调。编排走 Composite 协议（引用 gacha_single_reveal 实例），是 UI 层第一个 Composite。参数：rarities[10]、reveal_interval。
- **reward_fly_collect**：N 个奖励图标沿贝塞尔弧线错峰飞向目标栏位（起终点外部传入），到账时栏位脉冲+计数跳字光效（不做数字本身，只做光）。对象池协议：≤12 并发飞行元素复用。参数：item_count、arc_height、stagger。
- **daily_check_stamp**：印章图案缩放砸落+挤出墨圈+完成勾从笔画起点 Reveal 划出。参数：stamp_tint、ink_ring。
- **progress_charge**：条内横向流光循环（速度随 `fill_ratio`）；满档时端点星光溢出+整条呼吸一次。sustained，`fill_ratio` 运行期插值。参数：bar_rect、palette、fill_ratio。

## 3. 协议与预算

- 新增 **UI 交互挂点协议**：`anchor_rect`（RectTransform 引用）、`follow_mode`（static/follow）、多分辨率（16:9 与 19.5:9 双档验收，沿用 W14）。
- 运行时事件方法统一登记：`Play/Stop/SkipToReveal/SetFillRatio/SetRarity`，进错误码与 API 文档。
- 预算：UI 档同 W14（每效果 Canvas 元素 ≤24、材质 ≤3、无 ParticleSystem）；gacha 两项放宽至元素 ≤48（演出档，Manifest 声明）。

## 4. 批次验收
通用 DoD（Canvas 适用项）+ 附加：button 系在 3 种尺寸按钮上通用性验证；gacha 稀有度 5 档演出逐档录像+跳过截断演示；reward_fly 12 并发压力下对象池无泄漏（机器验）；全包在模拟界面上跑通一条"点按钮→开箱→奖励飞行→进度条满"的连续交互演示。

## 5. 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W17 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造按钮、卡牌、抽卡、奖励飞行或锚定的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。

## 6. 后续独立 next candidate（W24 全项目授权）

后续用户已另行授权全项目继续开发；该授权仅建立独立的 W17 下一候选，不撤销、不改写上方旧候选拒绝。新源码使用 `W17UiInteractionController` 与 `w17-ui-next-candidate/v1`，输出 id 全部带 `_next_candidate`，目标 Scene 为 `Assets/VFX/Preview/VFXPREVIEW_GameUI_NextCandidate.unity`，旧 Prefab、旧 Scene 与旧证据均不覆盖。

下一候选不再用统一旋转/缩放或状态标签代替表现：按钮按压与确认拥有真实 Rect 路径扫边和 3 尺寸适配；翻牌、合卡、宝箱、单抽/十连分别驱动独立 `RectTransform/Graphic` 拓扑；奖励飞行使用固定 12 槽对象池与真实贝塞尔位移；进度条由 `fill_ratio` 改变实际填充宽度。每个生产 Entry 内置 `RectMask2D` 硬裁剪，普通项 UI 元素 ≤24、gacha ≤48、ParticleSystem=0。

当前仅完成源码、稳定 Recipe/.meta、batch-safe authoring 与 Roslyn 静态编译；尚未运行隔离 Unity 的 Build/Edit/Preview/Play 门禁，也未进行用户视觉验收。唯一合法状态为 `NEXT_CANDIDATE_VISUAL_PENDING`，`userVisualVerdict=null`。
