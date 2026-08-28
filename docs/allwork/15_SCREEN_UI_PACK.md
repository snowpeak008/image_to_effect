# W14 屏幕 / UI 特效包（6 个特效）

> 实现状态（2026-08-25）：6/6 Canvas 语义 Runtime Entry、Recipe、语义 Patch、强度/叠层/锚点协议与双比例 Preview 已完成；正式条目零 ParticleSystem；用户已通过后续特效类批量签署拒绝当前视觉候选。未授权重做。

> 目标：在 damage_warning_ui 之外补全常用屏幕层反馈。全部走覆盖九宫格 B 定版的 Screen/UI 协议：Canvas 语义渲染、安全区/Mask 约束、不依赖 3D 相机 FOV；本批次同时受 W0 移交影响（若九宫格 B 第四轮 Screen/UI 被拒，其重做先行）。
> 批次预览场景：`VFXPREVIEW_ScreenUI.unity`（含 16:9 与 19.5:9 两种分辨率验收档）。

## 1. 清单

| id | 生命周期 | 一句话 | 关键分层 |
|---|---|---|---|
| heal_glow_ui | event | 治疗时屏幕边缘绿金柔光呼吸 1 次 | 边缘渐晕层、四角光叶上浮、中心不遮挡 |
| poison_veil_ui | sustained | 中毒状态：边缘毒紫脉络+气泡感 | 边缘脉络蔓延、缓速脉动、随毒层数加深 |
| levelup_burst_ui | event | 升级：底部金光柱升起+放射线+光尘 | 竖直光带、放射线旋转一周、金尘上升 |
| skill_ready_flash_ui | event | 技能就绪：图标位光环绽放+扫光 | 锚点定位环爆、边缘扫光一周、星闪 1 颗 |
| screen_shatter_transition_ui | event | 转场：屏幕碎裂为 20–30 块坠落 | 碎片多边形网格、错峰坠落旋转、露出底色/下一屏 |
| frost_creep_ui | sustained | 冰冻/极寒：四角冰晶向中心蔓延 | 四角冰纹 Reveal 生长、晶面高光扫过、解除时碎裂退场 |

## 2. 通用规格

- 参数：所有效果暴露 `intensity 0–1`、`palette`；状态类（poison_veil、frost_creep）另暴露 `stack_level 1–3`（蔓延范围随层数）。
- 锚定协议：skill_ready_flash_ui 接收外部 RectTransform 锚点；其余全屏效果只允许作用于边缘/角落安全区之外不超过屏幕短边 18%（中心可读性红线，机器可验：中心 60% 区域累计 Alpha ≤0.08）。
- screen_shatter 碎片网格确定性种子；两分辨率档各自验收（碎片不拉伸、边缘无缝）。
- 预算：UI 档 —— 每效果 Canvas 元素 ≤24、材质 ≤3、无 ParticleSystem（粒子感用 UI 元素池实现，沿用 Screen Archetype 规则）。

## 3. 批次验收
通用 DoD（Canvas 语义部分适用项）+ 附加：两分辨率档截图对比；中心可读性机器检查全绿；sustained 两效果的 stack_level 三档演示；screen_shatter 转场与场景切换驱动器演示一次（驱动器仅预览用）。

---

# 全部批次完成后（收尾里程碑，不单独立文档）

1. 汇总发布：CHANGELOG 0.2.0、模板/Recipe 清单再生成、错误码审计、A 系列验收矩阵扩展复跑。
2. VFX Studio Library 页签全量覆盖检查：91 个新族全部有缩略图、验收状态、风格变体入口。
3. 规则回写：本轮全部工程经验按 60/70 号规则文档归档。

## 用户视觉拒绝记录（2026-08-25）

用户决定后续特效类不再逐 Scene 验收，并将 W14 当前候选统一签署为：**拒绝，无法商用**。本 Scene 未作逐条/逐帧视觉核对，不据此伪造双比例、安全区或屏幕语义的单项问题；当前候选记为 `rejected`，未授权重做、修改源码/资产或生成下一候选。“无法商用”仅指视觉制作完成度。
