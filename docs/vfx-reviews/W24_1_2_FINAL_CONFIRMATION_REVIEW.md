# W24 1.2-draft 最终确认审查

> 评审对象：`docs/allwork/24_VFX_DESIGN_TO_IMPLEMENTATION_SYNC.md` 1.2-draft  
> 评审目的：授权 S0a 前的最终落实与一致性检查  
> 日期：2026-08-24  
> 最终结论：**GO-WITH-EDITS**  
> 边界：本次只审 1.2 落实情况和新增条款，未修改原计划、代码或资产。

---

## 1. 五项修订落实核对

### 1.1 ① 7.9 S0 最小合同扩为十段

**判定：落实正确。**

核对结果：

1. §7.9.1 已加入身份、版本与合同 hash。
2. §7.9.2 已加入 sustained 生命周期以及 start / steady / stop / interrupt 的入口和完成条件。
3. §7.9.3 已加入参考图 hash、来源、角色、权重和“不要求复制”的范围。
4. §7.9.4 已加入原点、朝向、锚点和实际游戏尺寸。
5. §7.9.5 已加入 Unity/URP、相机、分辨率、色彩与后处理、seed 等 Capture Profile。
6. §7.9.6 已加入完整语义状态机和 `continuityMode`。
7. §7.9.7 已补齐最小视觉层所需的 geometry、colorRole、blend、timing、attachment 等字段。
8. §7.9.8 已加入默认禁止替代和清理期限/允许残留。
9. §7.9.9 已加入最小预算。
10. §7.9.10 已加入 requirement 分类与状态/帧区间/ROI/Mask 证据定位。

非结构性小问题：§7.9.1 使用 `revision`，§4、§5、§21 使用 `contractRevision`。应统一为 `contractRevision`，但不影响十段结构本身成立。

### 1.2 ② 13.2b 四路证据与逐项降级

**判定：落实有偏差。**

正确落实的部分：

1. §0.1.2、§5、§11.2.7–8、§13 已建立 Beauty、Diagnostic Pass、Semantic Telemetry、Human Verdict Corpus 四路证据。
2. §13.2b 已逐项吸收上一轮评级表：
   - 碎片独立性改为 Telemetry 权威 + fragment-ID 交叉；
   - 随机火焰改为分布稳定而非强制周期；
   - 真实光照改为 receiver-ID + 线性 HDR A/B；
   - 清理改为 Mask、MAE、连通域和允许残留；
   - 拖尾改为 Telemetry 权威 + trail Mask 骨架；
   - 阶段连续性改为按 `continuityMode` 分类；
   - 多视角改为载体、Depth/Normal、视差和遮挡联合证明。
3. §13.2b 明确 S0 不引入 OpenCV，符合上一轮意见。

偏差：

1. §13.0 将全部 `visual requirement` 的权威证据统一写成 Diagnostic Pass，范围过宽。§14.1 中的主次、材质统一、模板感、参考意图偏离等属于视觉语义判断，无法由 Mask/ID/线性亮度作权威判定。
2. §13.2b 标题写“Diagnostic Pass 权威”，但其“碎片独立性”和“拖尾真实性”条款已明确 Telemetry 才是权威，标题与正文不一致。
3. “停止清理”“真实光照”“阶段连续性”同时包含内部事实与画面事实，应拆成两个不同 requirementId，各有唯一权威，而不是让一条 requirement 出现事实上的双权威。

必须修改为：每个 requirement 显式声明 `evidenceAuthority`，至少支持：

- `telemetry`：内部 behavioral / structural 事实；
- `diagnostic`：可量测画面事实；
- `visualQa`：可读性、主次、材质关系、视觉语义；
- `user`：审美和商用品质。

### 1.3 ③ S0a/S0b 拆分与盲测门槛

**判定：落实有偏差。**

正确落实的部分：

1. §17 已明确拆成 S0a 校准和 S0b 正式持续火焰，避免同一候选既训练又证明 QA。
2. §17 S0a 已包含受控 mutants、匿名 ID、独立 Holdout、提示/模型/filmstrip/聚合规则冻结。
3. false-pass、false-fail、uncertain、invalid、一致率和混淆矩阵指标均已写入。
4. §4 已规定 QA 未达到 S0a 门槛时只能 advisory。

偏差：

1. §17 S0a 的“退出条件”同时可能表示“QA 获得 L3 门禁权”和“盲测完成但 QA 仅 advisory”，没有定义两个独立终态。
2. §17.658 的缩减集只规定约 36 个 fail，没有规定 pass、uncertain、evidence-invalid 的缩减样本数，因此无法计算 §17.665–668 的全部指标。
3. §10.4 有五种顶层路由输出，但 §17.668 写“最终三态一致率”，未说明 per-requirement 三态与顶层五路输出如何分别计数。
4. S0a ground truth 没有进入 §13.0 权威证据表。参数 Patch 只能提供初始标签，最终标签应来自冻结、带 hash 的人工裁决清单。

### 1.4 ④ Capture 元数据与 hash

**判定：落实正确。**

§11.2.7 已正确加入：

- 带图形设备的 `-batchmode`，明确禁止 `-nographics`；
- `-UseGraphics` 必选；
- 正式场景和序列化相机；
- Unity/URP、图形 API/驱动、RenderTexture、Color Space、HDR/MSAA、Renderer/Volume、seed/fps 等环境冻结；
- 场景、Prefab GUID、Manifest/build hash、Capture 工具版本；
- diagnostic pass manifest；
- write-once 证据目录；
- 临时全帧与正式保留证据的存储边界。

§5.1 的候选包 hash 和 §19.4 的防造假规则与此一致。

### 1.5 ⑤ 完成定义的 visual / non-visual 拆分

**判定：落实有偏差。**

§21.815 已正确取消“所有 Required 都靠截图判断”，并将 behavioral / structural / budget 交给遥测和构建证据。

但同一句仍要求所有 visual requirement 都有“截帧与 Diagnostic Pass 判定”。这对色彩层级、材质统一、轮廓可读性、模板感和参考意图等定性要求不可执行。应与 §13.0 一并改成：

- 可量测 visual requirement：Diagnostic Pass 权威 + Beauty/QA 交叉；
- 定性视觉语义 requirement：Visual QA 权威 + Beauty 必需，Diagnostic Pass 可选；
- 审美/商用 requirement：用户权威。

另外，§21.816 的“0 漏检”必须限定为**冻结的已知规则测试集/校准集**，不能写成对未来未知作弊方式的绝对保证。

---

## 2. S0a 110/36 样本弹性规则

### 2.1 结论

**有条件接受。**

参数化批量生成优先、成本超限时先做较小样本，是合理的工程弹性；但缩减集不能获得与完整 110 样本相同的统计结论和 L3 门禁权限。

### 2.2 必须采用的分阶段口径

建议按完整集相同比例定义缩减集：

| cohort | 完整集 | 缩减集最低数 |
|---|---:|---:|
| visual fail | 60 | 36 |
| visual pass | 20 | 12 |
| visual uncertain | 20 | 12 |
| evidence invalid | 10 | 6 |

缩减集的作用：

- 校验生成器、标签流程、输入格式和 QA 提示是否基本可用；
- 允许提前发现高 false-pass 并停止浪费；
- 可以支持用户决定是否进入 S0b。

缩减集不能做的事：

- 不能宣称达到上一轮设计的约 5% false-pass 上界；
- 不能单独授予 Visual QA 正式 L3 门禁权。

36 个 fail 即使 0 false-pass，零事件估算的 95% 上界仍约为 8%–10%，明显弱于 60 个 fail 时约 5% 的目标。

### 2.3 成本可控的顺序检验方案

1. 先生成 36/12/12/6 缩减集。
2. 若出现任何 false-pass，立即停止扩样，先修 QA 协议。
3. 若缩减集为 0 false-pass，则允许 S0a 进入 `S0A_ADVISORY_ONLY`，由用户决定是否在 advisory 模式下推进 S0b。
4. 再增补 24/8/8/4，达到 60/20/20/10 完整 Holdout。
5. 只有完整 Holdout 达标时进入 `S0A_GATE_QUALIFIED`，Visual QA 才获得 L3 门禁权。

该方案接受文档的成本弹性，同时不牺牲门禁结论的统计含义。

### 2.4 标签权威

应新增冻结的 `calibration-labels.json` 或等价清单，包含：

- sampleId；
- ground-truth route / per-requirement label；
- 标签来源和审核者；
- 是否可见；
- requirementId；
- 证据 hash；
- 裁决版本与清单 hash。

不可见的注入错误从视觉混淆矩阵剔除，转到 behavioral / structural 测试，不得为了凑 fail 数保留为视觉 fail。

---

## 3. 新矛盾与术语一致性

### 3.1 §13.0 与 §13.2a/13.2b

**存在矛盾，需修改。**

1. §13.2b 标题称 Diagnostic Pass 权威，正文却对碎片/拖尾指定 Telemetry 权威。
2. §13.0 的 visual 行覆盖过宽，无法包含 §14.1 中的定性视觉要求。
3. 同一复合要求可能在 §13.2a 和 §13.2b 各出现一个权威结论。

修正方式：按单条 requirement 的 `evidenceAuthority` 路由；复合要求拆 requirementId；§13.2b 改名为“渲染量测：可量测视觉事实的权威证据 / 内部事实的交叉证据”。

### 3.2 §5.1 回路状态图与 §10.4 输出枚举

**基本对应，但不完全闭合。**

需要修正：

1. 状态图使用 `QA pass / implementation fail`，§10.4 使用 `VISUAL_PASS / VISUAL_FAIL`；应统一精确枚举。
2. 应加入 `MACHINE_FAIL → C{n+1}`，机器门禁失败时不应进入 QA。
3. `EVIDENCE_INVALID` 第二次重采仍失败没有终态；建议输出 `CAPTURE_BLOCKED → NEEDS_USER_DECISION`。
4. `CONTRACT_AMBIGUOUS` 可以无限升 revision、绕开 C0/C1/C2；应规定同一 effect 连续重开合同需要用户确认。
5. C2 后的 `NEEDS_USER_DECISION` 是工作流/聚合器结论，不是 Visual QA 输出；应明确发出者。

### 3.3 三态与五路输出

**当前不自洽。**

应明确：

- per-requirement 三态只统计 `pass / fail / uncertain`；
- 顶层路由单独统计 `VISUAL_PASS / VISUAL_FAIL / EVIDENCE_INVALID / CONTRACT_AMBIGUOUS / VISUAL_UNCERTAIN`；
- `EVIDENCE_INVALID` 计算独立召回率；
- `CONTRACT_AMBIGUOUS` 要么加入专门样本，要么声明 S0a 首轮不评该指标。

### 3.4 S0a 退出条件与 §4、§14 权限

**存在直接矛盾，必须先改。**

§4.111 允许 QA 未达门槛时降为 advisory；§5.1 要求 `VISUAL_UNCERTAIN → 用户`；但 §14.2 又规定用户看到的候选必须先通过 §14.1。这样会导致 advisory 模式和 uncertain 升级都无法真正到达用户。

应定义两种 S0a 终态：

- `S0A_GATE_QUALIFIED`：完整盲测达到门槛，QA 获得 L3 门禁权；
- `S0A_ADVISORY_ONLY`：盲测完成但样本不足或指标未达标，QA 报告必跑但不阻断；进入 S0b 需用户显式授权。

§14.2 应改为：

- 普通 L3→L4 签署入口要求 `VISUAL_PASS`；
- `VISUAL_UNCERTAIN`、advisory 模式、校准争议复核属于有醒目标记的用户升级入口，不受“必须先 pass”限制。

§4 的 L3 定义也应同步说明：QA 未获门禁权时不能生成普通 L3；用户可以走显式 override/预筛路径，但不能把 advisory 报告写成 QA pass。

### 3.5 校准标签与证据权威

**缺少条款。**

§13.0 应增加 S0a 专用行：校准 ground truth 的权威证据是冻结、带 hash 的人工标签清单；Patch 注入信息只是初始标签和可追踪来源。

### 3.6 术语小修

1. `revision` 与 `contractRevision` 统一为后者。
2. `non-visual fail` 不是正式 requirement 类型，应改为 behavioral / structural / budget 中的具体类型。
3. §21 的“0 漏检”限定为 frozen known-cheat corpus。
4. §10.4.440 的异常示例应分别放入 `EVIDENCE_INVALID`、`CONTRACT_AMBIGUOUS`、`VISUAL_UNCERTAIN`，不要在一个“必须 uncertain / invalid”列表中混写。

---

## 4. 最终结论

### GO-WITH-EDITS

1.2 已正确完成主要架构修订，**不存在需要推翻方案的 NO-GO 问题**；但在授权 S0a 前必须修改以下条款：

1. **§13.0、§13.2b、§21**：增加 per-requirement `evidenceAuthority`；区分可量测视觉事实、视觉语义和用户审美；修正 13.2b 标题；拆分复合 requirement。
2. **§17 S0a、§4**：定义 `S0A_GATE_QUALIFIED` 与 `S0A_ADVISORY_ONLY`；缩减集不得直接授予 L3 门禁权。
3. **§17 S0a**：为缩减集明确 36 fail / 12 pass / 12 uncertain / 6 invalid；补冻结、带 hash 的人工标签清单。
4. **§10.4、§17 S0a**：分别定义 per-requirement 三态指标和顶层五路路由指标。
5. **§14.2、§5.1、§4**：允许 `VISUAL_UNCERTAIN`、advisory 与校准争议进入带标记的用户升级路径，消除“必须先 VISUAL_PASS”冲突。
6. **§5.1、§10.4**：统一精确枚举，补 `MACHINE_FAIL`、二次采集失败终态和合同反复重开的边界。
7. **全文术语**：统一 `contractRevision`，限定“0 漏检”为冻结已知测试集。

完成这些文字修订并复核一致性后，可提请用户授权 S0a；无需再做第三轮架构重写。

