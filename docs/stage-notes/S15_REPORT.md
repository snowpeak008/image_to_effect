# S15 Slash 最终视觉报告

> 日期：2026-08-24  
> Scene：`Assets/VFX/Preview/S12_SlashGeneratedPreview.unity`  
> 用户结论：**有条件通过**  
> 用户原话：**“内容还可以，但无法做商用”**

## 1. 签署边界

本报告只记录用户对 S15 Slash 的最终视觉结论，不将它扩写为无条件视觉通过或商用认可。用户未指定具体失败时间阶段，也未给出解除限制的条件。“无法做商用”的原因未由用户指定；本报告不推断其源于画面质量、许可、来源或其他原因。本次签署也不授权重做或修改源码/资产。

源码与机器门禁已完成，但机器结果不替代上述用户结论。

## 2. 原计划验收范围

- 起点同步与左下角点火。
- 宽弧面由左下向右上扫出。
- 黄白主刃、橙黄主体、红色余焰/碎片与稀疏火星的层级。
- 残留持续衰减，最终清空，不闪回。

## 3. Authority 帧证据索引

保留序列：[`run-20260823T041806959Z`](s15-wysiwyg-evidence/run-20260823T041806959Z/metadata.json)。该序列由同一序列化 MainCamera、真实 PlayMode player loop 和一次 `PlaySlash` 记录；它是唯一 authority 审查证据，不自动构成视觉通过。历史试录只保留在 `s15-wysiwyg-evidence/rejected-runs/`，不得作为替代 authority。

| 原计划时点 | Authority 帧 |
|---|---|
| `.016–.033` | `frame_0001.png`、`frame_0002.png` |
| `.10` | `frame_0006.png` |
| `.166` | `frame_0010.png` |
| `.233` | `frame_0014.png` |
| `.333` | `frame_0020.png` |
| `.416` | `frame_0025.png` |
| `.45/.451` | `frame_0027.png`（序列末帧 `.45`） |

辅助审查文件：`slash-s15-standard-realtime.gif`、`slash-s15-standard-keyframes.png`、`metadata.json`。

只读派生机器量测 v2：[`projection-metrics.json`](s15-wysiwyg-derived/run-20260823T041806959Z-projection-audit-v2/projection-metrics.json) 与 [`PROJECTION_AUDIT.md`](s15-wysiwyg-derived/run-20260823T041806959Z-projection-audit-v2/PROJECTION_AUDIT.md)。独立复审已否决 v1 的 component-to-particle 归因；v1 原字节仅作为 `SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO` 历史记录保留，不再是活动证据。

V2 绑定并复验原 `metadata.json` 与 28 帧 SHA-256，不修改原证据。RGB 只输出 detached warm candidate component 数量和候选画布边距，不把候选归因为 spark。所有 spark/dissipation live readback 均只计入 `unattributedRecorderLiveCount`；例如 `frame_0020` 的一个 spark、三个 dissipation 与一个暖色候选之间不建立对应或比例。检测到的候选像素最小画布边距为 `148 px`，但真正 off-canvas、spark 可见性、遮挡及主刃重叠均因缺少 instance-ID/depth 诊断层保持 `PENDING_INSTANCE_ID_OR_DEPTH_DIAGNOSTIC`。该量测不改变“有条件通过但不可商用”的用户结论。

## 4. 回写状态

- `FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md`：S15 已记为用户“有条件通过”并保留商用限制原话。
- `S15_VISUAL_DELTA_AND_TECHNICAL_PLAN.md`：已结束 `awaiting visual review` 状态，但未声明无条件通过。
- `docs/allwork/03_LEGACY_VISUAL_CLOSURE.md`：W0 其余三项尚未签署，本次不提前关闭。
- `docs/allwork/00_INDEX_AND_ACCEPTANCE.md`：全计划尚未全部签署，本次不写“全计划视觉完成”。
