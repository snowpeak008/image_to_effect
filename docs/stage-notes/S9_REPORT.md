# S9 阶段纪要：Codex 正式工作流

> 状态：**通过主 Agent 独立验收，S9 完成。** Cohort J 历史终局结果 2/3；Cohort K 终局 3/3；正式 M6 已通过。未进入 S10；未修改默认 Recipe、S7 Preview Scene 或保留 Generated Prefab。

> 独立验收记录：**2026-08-22 20:34:45 +08:00**。主 Agent 复跑确认 compile 0 error；EditMode **96 total / 61 passed / 0 failed / 35 skipped**；PlayMode **4/4**；清理干净。结论：S9 完成，S10 未获本次任务授权，不进入。

## 全量测试生命周期整改

主 Agent 首次全量 EditMode 实跑得到 **96 total / 8 failed**。失败项均为已越过生命周期的证据测试，而非产品回归：G/H/J historical final evidence 为失效、noncountable 的历史审计；I/J/K preregistration 只允许在 attempt0 前运行；J/K initial、repair 与 terminal recorder 是一次性证据持久化步骤。它们现均以带原因的 `Explicit` 标记保留，防止正常全量回归重复写入历史证据或把已知历史失败重新计为产品失败。K final evidence、formal M6，以及 exporter/domain/compiler/patch/runtime 正式产品测试保持 non-Explicit。该整改只消除生命周期误运行，**不表示产品或 S9 已自动通过**；仍需主 Agent 独立验收 K 3/3 与 M6 证据链。

整改后复跑：compile 通过；全量 EditMode 为 **96 total / 0 failed / 35 Explicit skipped**，结果门禁确认 total>0 且 failed=0；PlayMode 为 **4 total / 0 failed**。复跑后 `Assets/VFX/Generated` 仅有 `fireball_2d`，且没有 `s9_` Recipe、history 或 Generated 残留。

## 已完成的正式工作流与审计事实

- `docs/ai-workflow/` 包含 Recipe、报告与 Patch 规范。参数表、canonical Recipe 与 canonical Patch examples 均由正式 Recipe、live Catalog/Manifest 导出；Patch examples 会对隔离副本真实调用 `VfxPatchService`，核验 revision `1->2`、history 与效果，且内容未变不重写。J 预注册测试还机器拒绝任何与 J 完整 operation 或同类型目标路径/值/ID/参数组合重合的生成示例。
- Patch-only 作者的最小阅读顺序是 Patch 规范、canonical revision-1 Recipe、生成的 Patch examples；仅在 `add` 或参数替换时读取 Catalog 表。它不包含完整 Recipe Schema。
- Cohort A–I 均作为历史审计保留。Cohort I 的冻结文件、证据和测试没有重写：Recipe 真实结果为 **4/5**；Patch 真实结果为 **1/3**（仅 P2 成功，P1 与 P3 失败）。原 I 全批次门禁保留为 Explicit historical audit，以免常规 EditMode 因已知 Patch 失败变红。
- 旧 A 的 Recipe 3/5 与任何“Patch 3/3”表述均不是当前完成结论，也不能用于 M6。

## Cohort J（已派发，终局 2/3，历史失败保留）

J 是新的 Patch-only 恢复批次，精确包含 J1–J3：launch `launchFlash` size 改为 1.4、disable impact `shockwave`、以及 travel 添加 `linger_embers`。其 acceptance spec、短 Patch-only payload、temp hash-bound envelope 与 hash manifest 均在派发前冻结，之后没有改写。

J 的最终证据测试现为 **Explicit historical**：它逐项累计失败而不中断，且只在最终断言 **3/3**，因此不参与正常产品回归或恢复后的 M6 计数。每项核验完整链路、envelope/payload/temp hash、thread continuity、final 与最后 attempt 的字节一致性；随后对隔离 Recipe 真实 Apply，核验 revision/history `1->2` 与预注册效果。实际结果是 **J1 成功、J2 成功、J3 失败（2/3）**：J3 在 terminal repair2 仍返回错误报告数组而非 Patch，真实报告为 `E702`。没有第四轮 repair。

J 首发后发现报告实现缺口：此前报告只把 `VfxPatchService.ApplyToAsset` 的技术成功当作成功，未将已经冻结的 J acceptance operation/path/value/effect 重新核验。该实现已在首发报告前修正：合法但错目标的 J1 真实 Apply 为 `1->2`，但报告以 `E720`（冻结路径、实际 operation、期望 operation）记为失败并进入 repair；J2 为真实成功；J3 保留真实 Patch shape 失败。此修正不改动任何 J 冻结输入或最终验收。

repair1 后，J1 再次复现错目标 operation，J3 返回空数组。技术错误报告虽已准确记录失败，但旧 repair payload 只携带前报告，未完整突出冻结的目标 operation；因此仅对后续 repair payload 增强为“完整前机器报告 + 从冻结 acceptance-spec 机器生成的 authoritative complete bare array”，并明确不得复制 canonical example。主 Agent 核验后亲自发送 terminal repair2；J1 修正成功，J3 仍失败。

## Cohort K（终局 3/3，待独立验收）

K 是在不改写 I/J 任何证据的前提下新增的 Patch-only 恢复批次，精确包含 K1–K3：impact `burst` speed 替换为 4.4、disable launch `launchFlash`、以及 travel 添加 `sparkle_embers`（`PFT_2D_Embers`、`core`、rate 12、lifetime 0.65）。三个 payload 由正式 canonical Recipe 与 live Catalog 机器提取相关 stage/module 和参数类型/范围，首段为 TASK，且只含受限操作语法、稳定 ID-based `PATH_RULES`、相关 Recipe 上下文、必要 Catalog facts 与 `OUTPUT=bare array`。PATH_RULES 固定 replace/disable/add 的 stable paths，要求 add 的完整 module value/id 一致，并禁止数组下标、wrapper、Markdown、prose/fence。它们均小于 3500 bytes，存在每 key 独立 temp byte-pair/hash envelope；K 不包含 canonical Patch examples、完整 Recipe/schema/参数表或其他 stage/module。

K 的协议、acceptance、prompt、freeze、pre-reg 和最终非-Explicit exact 3/3 gate 已在派发前完成。每条 final chain 必须保存 succeeded sequence、同 thread（最多两次 repair）、envelope/payload/temp hash、repair prepared/hash、final bytes，并在隔离 Recipe 上真实 Apply 验证 revision/history 1->2 与冻结 effect。Repair 从第一次准备起固定包含完整 prior report 与由冻结 acceptance 生成的 authoritative bare operation，成功后会拒绝 repair。

K 初始输出已按冻结 witness 记录：K1 的 `burst.speed=4.4` 与 K2 disable `launchFlash` 都真实 Apply 成功、revision/history `1->2` 且 attempt0/final bytes 相同；二者没有 repair。K3 原始输出带 `attachedTo` 而非 `attachTo`，真实报告为 `E100` unknown field、revision `1->0`，保留为失败。主 Agent 在同一 child thread 发送了已独立核验的 repair1 envelope；修正输出真实 Apply 成功、revision/history `1->2`、attempt1/final bytes 相同，未准备或发送 repair2。非-Explicit K final evidence gate 现已通过 exact **3/3**，并真实重放验证每条 revision/history/effect；正式 M6 gate（I Recipe >=4/5 + K Patch 3/3）同样通过。K 的真实 Apply 临时 Recipe/history/Generated 均已精确清理，保留 Generated 只有 `fireball_2d`。所有 transport witness 均披露宿主无法读取 wire payload 或 child tool trace。

正式恢复 M6 门禁已更换为非-Explicit **I Recipe >=4/5 + K Patch 3/3**。这是新的强门禁，不是对 J 2/3 的篡改：历史 I Patch 1/3 与 J Patch 2/3 都明确不计入恢复门禁，也仍保留其失败事实。

## 传输限制披露

短 envelope、temp/workspace byte-pair、SHA-256 和主 Agent witness 只能证明本地准备与见证。宿主不提供 wire payload 或子 Agent tool trace readback；每个 transport record 必须继续保留这一限制披露。

## 当前停止点

主 Agent 已独立验收 J 的真实 2/3 历史失败、K 的终局 3/3 完整证据链，以及新的 M6 强门禁。S9 在此结束；不得基于本次任务进入 S10。
