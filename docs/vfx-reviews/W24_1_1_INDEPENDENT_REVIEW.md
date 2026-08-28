# W24 1.1-draft 独立评审意见

> 评审对象：`docs/allwork/24_VFX_DESIGN_TO_IMPLEMENTATION_SYNC.md` 1.1-draft  
> 评审日期：2026-08-24  
> 评审性质：独立设计与技术可行性审查；未修改原计划、项目代码或资产  
> 总结结论：**有条件通过（Conditional GO），不建议按 1.1 原文直接实施。**

---

## 1. 整体判断

### 1.1 结论

1.1 版能够显著缓解“设计—实现—视觉验收不同步”，因为它第一次把真实画面放入正式反馈回路，并且把用户从“第一双看到画面的眼睛”改成最终签署者。这三项修订是有效的：

1. 视觉 QA 真正读取关键帧和 filmstrip，而不是只读文字与组件数据。
2. 实现失败可以带着具体帧号和偏差返回实现者，不再等用户指出所有初级错误。
3. 先打穿持续火焰垂直切片，再反推完整 Schema，能够降低“先造大而全基础设施、最后画面仍然失败”的风险。

但这套方案只能建立**更可靠的生产与拦截流程**，不能单独创造商用审美能力。Design Skill、视觉 QA 和像素门禁即使全部完成，也只能做到：

- 设计意图被显式记录；
- 语义偷换更容易被发现；
- 明显错误更早被拦截；
- 用户拒绝意见可以积累成项目经验。

它不能保证：

- AI 自动拥有成熟 VFX 美术总监的构图与质感能力；
- 对任意参考图都能稳定产出商用品质；
- Visual QA Agent 对“高级感、风格统一、商业竞争力”的判断可靠。

因此，W24 的正确目标应是“**设计可执行、实现可追踪、明显错误不过门、用户决定最终品质**”，而不是“自动保证商用级视觉”。

### 1.2 最有效的部分

1. L0–L4 的真实状态分级有效，尤其是“未看真实截帧最多 L2”。
2. `designRequirementId` 从合同追踪到实现和截帧判定有效。
3. 禁止静默替代有效，这是此前错误的主要来源之一。
4. 视觉 QA 与实现者隔离有效，能减少实现者自我合理化。
5. S0 先打穿一条火焰基线有效，符合当前项目真实短板。
6. 用户签署权保持不变，权限划分正确。

### 1.3 无效、过度或需要修订的部分

1. **普通 RGB 截帧不应承担所有隐藏语义证明。** 光流、亮度和帧差可以作为辅助信号，但不能可靠证明“独立碎片”“真实光照”“真实拖尾”等内部事实。
2. “组件门禁、像素门禁、视觉 QA 至少两层拒绝”不应机械计数。三个层可能共享同一错误假设。正确口径应是“一个权威语义证据 + 一个独立交叉证据”。
3. 完成定义中的“100% Required 设计要求都有对应截帧判定”过度。预算、seed、事件、GUID、对象池清理等要求不可仅凭截图判断。必须拆成 `visualRequirements` 与 `nonVisualRequirements`。
4. 视觉 QA 直接对照参考图存在误区。参考图必须标注角色和权重，例如只参考构图、色彩、材质或运动，不得默认要求像素级相似。
5. “视觉 QA 看画面本身，所以不依赖预定义规则”表述过强。视觉模型仍依赖提示词、参考、训练偏差和输入分辨率；它只是开放式补充防线，不是未知错误的可靠检测器。
6. 对 250+ 既有条目批量初筛可以使用视觉 Agent，但不能直接产生 L0–L4 最终分级。最多输出风险分和抽样建议，因为不同条目的合同、相机、生命周期与参考完整度并不一致。

---

## 2. 13.2b 像素量测可行性

### 2.1 Unity 截帧基础条件

**结论：可落地，但必须定义为“有图形设备的 Batch Mode”，不能使用项目默认的 `-nographics`。**

依据：

- Unity 2022.3 的 `-nographics` 明确表示不初始化图形设备，不能作为 URP 渲染证据环境。[Unity 2022.3 Editor 命令行参数](https://docs.unity3d.com/cn/2022.3/Manual/EditorCommandLineArguments.html)
- Unity 支持 Camera 渲染到 RenderTexture，再通过 `Texture2D.ReadPixels` 回读 CPU。[Unity 2022.3 RenderTexture](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/RenderTexture.html)
- `Time.captureFramerate` 可以让 Update 以固定模拟时间间隔运行，适合离线连续采集。[Unity 2022.3 Time / Capture frame rate](https://docs.unity3d.com/cn/2022.3/Manual/TimeFrameManagement.html)
- 项目已有 `tools/Invoke-Unity.ps1 -UseGraphics` 开关；不传该开关时默认加入 `-nographics`。
- 项目已有 S14 连续捕获实现，能够在真实 PlayMode Update 下使用正式序列化相机采集。
- 项目历史报告已经记录 URP 粒子在 `-nographics` 下 `Camera.Render` 崩溃，因此这不是理论风险。

建议把视觉作业固定为：

```text
Unity.exe -batchmode -projectPath <project> ...
```

并明确**不加** `-nographics`，使用隐藏窗口而不是无图形设备模式。

还必须冻结：

- Unity 版本与 URP 版本；
- 图形 API 与显卡/驱动标识；
- 分辨率、RenderTexture 格式、sRGB/Linear、HDR、MSAA；
- Camera、Renderer Asset、Volume、Bloom、Tone Mapping；
- seed、fps、场景与 Prefab/Manifest hash。

当前项目 `m_ActiveColorSpace: 0`，且现有捕获大量使用 `ARGB32 + PNG`。这类 LDR 输出适合视觉 Agent 看图，却不适合直接作为物理亮度的唯一量测来源。

### 2.2 各项评级

| 13.2b 项目 | 原定义评级 | 理由 | 建议替代或降级方案 |
|---|---|---|---|
| 碎片独立性：稀疏光流方向离散度 | **需降级** | 光流可实现，但透明、Additive、发光、粒子生成/消失会违反亮度恒定和邻域同运动假设。OpenCV 官方也明确光流依赖这些假设；同一刚体旋转在不同位置本来就具有不同方向，单看“方向离散度高”会把整体旋转误判成独立碎片。[OpenCV Optical Flow](https://docs.opencv.org/4.x/d4/dee/tutorial_optical_flow.html) | 组件读回作为权威证据；另加 object-ID / fragment-ID 诊断 Pass，在屏幕空间跟踪各 ID 的质心、角度和轨迹相关性。普通光流只输出诊断图，不直接决定 pass/fail。 |
| 循环稳定性：帧差周期检测 | **需降级** | 可计算，但持续火焰通常应“统计稳定”而非像 GIF 一样严格重复。随机火焰无固定周期仍可能是正确效果；严格周期反而可能鼓励可见循环接缝。 | 对明确周期素材使用自相关/频谱检测；对随机稳态火焰改用三个稳态窗口的面积、亮度分位数、质心、粒子数分布和线性漂移比较。门禁目标是“分布稳定、无长期漂移”，不是“必须有周期”。 |
| 真实光照：接收区 PNG 亮度直方图 | **需降级** | A/B 对比原则正确，但普通 Beauty PNG 会受 Gamma、Tone Mapping、Bloom、Additive 主体泄漏和透明排序影响，不能独立证明 Light。 | 使用同一相机矩阵的 receiver-ID Mask + 线性 HDR 诊断 RenderTexture；固定曝光，A/B 只切换 Light，不改变模拟状态。计算接收物区域线性 luminance 差；Beauty 帧只供视觉 QA。组件层仍要求实际 Light/Light2D。 |
| 停止清理：末帧与空场景差分 | **可行，带条件** | 在固定相机、固定背景、无动态后处理、无环境动画时，差分稳定且实现简单。NumPy/Pillow 已足够，不需要 OpenCV。 | 合同必须先声明 `cleanupDeadline` 与 `allowedResidualLayers`。用 effect-only Mask 或按层 Mask 比较；若合同允许烟或 Decal 残留，不能要求整帧回到空场。建议用归一化 MAE + 残留连通域面积，而不是逐像素完全相等。 |
| 拖尾真实性：像素演变与发射体轨迹一致 | **需降级** | 原理可行，但透明拖尾、宽度变化、渐隐和遮挡使直接相关难以设统一阈值；静止期间旧拖尾继续收缩/淡出也会产生合法变化。 | TrailRenderer/Particle Trail 组件读回是权威证据。渲染侧使用 trail-only Mask，比较其骨架与发射体历史投影的走廊覆盖率、Hausdorff 距离或平均最近距离；静止期只禁止“头部继续向新空间增长”，允许尾部淡出。 |
| 阶段连续性：切换前后全画面帧差 | **需降级** | 全画面帧差对爆发、镜头光、Bloom 和合法瞬发极敏感，也可能漏掉局部硬切。一个统一阈值不可复用。 | 合同为每个 transition 声明 `continuityMode`：continuous / impulse / replace / clear。对 continuous 使用逐层 Mask IoU、锚点位移、面积/能量变化率；对 impulse 允许大帧差，但仍检查指定连续锚点或残留层。 |
| 多视角一致性：两个视角都有轮廓 | **需降级** | 可采集且可量测，但“两个视角都有像素”不能证明是真 3D；Camera-facing Billboard 在两个视角都可见。 | 结合载体读回、Depth/Normal 或 object-ID Pass。若合同要求空间体积，检查视角变化后的轮廓变化、深度跨度、遮挡关系或锚点视差；若 Billboard 是允许载体，则不应以“非 3D”拒绝。 |

### 2.3 工具依赖结论

本机当前环境：

- `numpy`：存在；
- `Pillow`：存在；
- `cv2 / scipy / skimage`：不存在；
- `tools/vfx` 当前只有 Frost Atlas 脚本，没有 13.2b 工具集。

因此：

- 帧差、直方图、连通域基础统计、周期自相关可以用 NumPy/Pillow 自建；
- 稀疏/稠密光流若坚持使用，需要新增 OpenCV 依赖或自行实现；
- S0 不应为了光流先安装大型依赖，建议先采用 ID/Mask + 组件遥测方案。

### 2.4 捕获性能与存储

同步 `ReadPixels` 会产生 GPU→CPU 等待，但对于离线短片 S0 可接受，可靠性高于过早引入异步管线。Unity 2022.3 也提供 Async GPU Readback，可在后续优化，但请求跨帧完成、Editor Player Loop 与错误处理更复杂。[Unity 2022.3 AsyncGPUReadbackRequest](https://docs.unity3d.com/cn/2022.3/ScriptReference/Rendering.AsyncGPUReadbackRequest.html)

建议：

- 全 60fps 原始帧只存系统临时目录，分析后删除；
- 正式证据只保留关键帧、filmstrip、指标、hash 和必要的低码率预览；
- 机器分析用全帧，视觉 Agent 用分段 filmstrip + 关键帧，避免一张超长联系表信息过密。

---

## 3. 视觉 QA Agent 三态校准

### 3.1 结论

不能用最终那一份持续火焰候选同时“校准 QA”又“证明 QA 有效”，否则会过拟合。S0 应拆成：

- **S0a：校准夹具和盲测**；
- **S0b：真正的持续火焰生产切片**。

### 3.2 校准集

为持续火焰制作受控变异（mutants），每个变异只注入一个明确错误：

1. 整体纹理/整组层同步旋转；
2. 稳态亮度持续漂移；
3. 循环接缝或周期跳变；
4. 停止后粒子残留；
5. 停止后 Light 残留；
6. 烟层遮挡主体；
7. 火焰核心与外焰层级颠倒；
8. 点燃阶段缺失；
9. 停止阶段硬切；
10. Additive 假光但接收物不变；
11. 相机裁切或尺寸错误；
12. 证据帧缺失、顺序错或元数据不一致。

每类至少生成：

- 2 个明显失败强度；
- 1 个边界强度；
- 2 个固定 seed。

建议最低集合：

- 已知 fail：60 个；
- 已知 pass：20 个；
- 应判 uncertain 的边界/证据冲突：20 个；
- evidence invalid：10 个。

这些条目以匿名随机 ID 提交，文件名不得暴露 pass/fail 或缺陷类型。

### 3.3 标签来源

1. 明确注入的结构错误提供初始 ground truth。
2. 但如果错误在最终画面不可见，标签不能自动算视觉 fail，应由独立人工查看后改为 uncertain 或 non-visual fail。
3. 用户不需要逐条审核全部夹具，但应审核每类至少一个强失败、一个边界案例，以及所有存在争议的标签。

### 3.4 冻结与盲测

在正式盲测前冻结：

- QA Agent 提示和版本；
- 模型版本与图像输入策略；
- 设计合同版本；
- filmstrip 布局、分辨率与帧表；
- 三态规则和总体结论聚合规则。

校准集用于修改提示；校准完成后另生成盲测 Holdout，不得再用 Holdout 调提示。

### 3.5 指标

三态系统不能只看 accuracy，应报告：

- `false-pass rate`：真实 fail 被判 pass；这是最高优先级指标。
- `false-fail rate`：真实 pass 被判 fail。
- `abstention rate`：输出 uncertain 的比例。
- `conditional accuracy`：排除 uncertain 后的正确率。
- 按 `designRequirementId` 类型拆分的混淆矩阵。
- 同一证据重复审查的一致率；对抽样集运行 3 个全新隔离会话并计算一致性。

S0 建议门槛：

- 60 个已知视觉 fail 中 **0 个被放行为 pass**；若做不到，Visual QA 只能是 advisory，不得作为 L3 门禁。
- 已知 pass 的 false-fail 不高于 10%。
- 非边界集 uncertain 不高于 15%。
- 10 个 evidence-invalid 识别率 100%。
- 重复审查的最终三态一致率不低于 90%。

“60 个 fail 中 0 false pass”并非证明未来错误绝不漏检，但在零事件近似下，其 95% 上界约为 5%，至少提供一个可解释的 S0 校准尺度。

### 3.6 `uncertain` 的规则

不使用模型自报的数字置信度作为主要依据。以下情况必须 uncertain 或 evidence-invalid：

- 画面分辨率不足；
- 参考目标冲突；
- filmstrip 无法区分运动方向；
- required 要求在截图中本来不可观察；
- 合同视觉描述有多个合理解释；
- 关键区域被 Bloom、遮挡或裁切，无法判断。

---

## 4. S0 最小合同审查

### 4.1 结论

7.9 的四段**方向正确但不够用**。它能够描述状态、层和禁止替代，却没有固定“看见什么画面”的实验条件。没有相机、色彩、尺寸、seed 和参考角色，视觉 QA 与像素量测无法复现。

### 4.2 必须提前进入 S0 的字段

1. **身份与版本**：`contractVersion`、`effectId`、`revision`、合同 hash。
2. **生命周期**：`sustained`、start/steady/stop/interrupt 的入口、期限和完成条件。
3. **参考角色**：参考图 hash、来源、只参考构图/颜色/材质/运动中的哪几项，以及明确不要求复制的内容。
4. **空间与尺寸**：原点、朝向、锚点、设计尺寸、游戏距离尺寸。
5. **Capture Profile**：Unity/URP 版本、相机、分辨率、fps、背景、Color Space、HDR、MSAA、Bloom、Tone Mapping、seed。
6. **视觉层的必要表现字段**：在 7.5 现有最小项上补 `geometry`、`colorRole`、`blendMode`、`timing`、`attachment`。
7. **清理语义**：`cleanupDeadline`、`allowedResidualLayers`、Light 停止期限。
8. **最小预算**：粒子峰值/稳态、Renderer、Material、Light、Texture 驻留预算。完整 CPU/GPU Profile 可后补。
9. **Requirement 分类**：visual / behavioral / structural / budget，避免要求所有项都由截图判定。
10. **证据定位**：每个视觉要求对应状态、帧区间、ROI 或层 Mask，而不是只有一句自然语言。

### 4.3 S0 可以暂时砍掉的字段

- 完整目标平台矩阵，只固定当前 PC Editor/Windows Player 目标即可；
- Small/Standard/Hero 多质量档，只做一个固定档；
- 完整 Shader Variant、Overdraw、GPU 时间正式阈值，S0 先 report-only；
- 与火焰无关的模型/骨骼/命中法线字段；
- 大量 `allowedSubstitutions`，S0 可采用“默认不允许，逐条白名单”；
- 完整通用 Schema 的所有 Archetype 分支。

---

## 5. 回路防空转规则审查

### 5.1 原规则评价

“最多 3 轮、截帧只能自动产出、不确定升级用户”方向正确，但仍有漏洞。

### 5.2 漏洞与修正

1. **“一轮”没有定义。** 建议定义为一个不可变候选包：合同 hash + Prefab/Manifest hash + Capture 工具 hash + 证据 hash + QA 报告。建议最多 3 个候选 `C0/C1/C2`，不是初版后再无限附加 3 次修复。
2. **合同可能在修复中被偷偷改变。** 进入 C0 后合同冻结；若改变 required 要求、参考角色或禁止替代，必须新 revision，并回到 Design Director/用户，不算同一修复回路。
3. **自动截帧不等于真实截帧。** 必须验证场景、相机、Prefab GUID、Manifest/build hash、Capture 工具版本、帧号、模拟时间和 seed；证据目录 write-once。
4. **只看一个 seed 可以挑最好结果。** S0 至少使用一个 canonical seed + 两个固定 robustness seed；用户可只看 canonical，机器与 QA 必须检查三者没有明显崩坏。
5. **Visual QA 会被上一轮解释锚定。** 每一候选使用全新隔离 QA 会话，先只看当前合同和当前证据；聚合器在审查完成后再比较历史差异。
6. **修一个问题可能破坏已通过项。** 每轮必须重审全部 required 项，并产生 per-requirement regression diff，不能只复查上轮 fail。
7. **3 轮可能都在错误设计上打磨。** 若 C0 出现载体级或合同级根本冲突，不应消耗 C1/C2，立即退回设计。
8. **uncertain 会把大量噪声推给用户。** 先区分 `EVIDENCE_INVALID`、`CONTRACT_AMBIGUOUS`、`VISUAL_UNCERTAIN`；前两者分别退采集或设计，只有真正的视觉歧义升级用户。
9. **像素指标可能被针对性优化。** 禁止只优化 fail 数值；Beauty 帧、诊断 Pass、组件遥测和整体视觉 QA 必须同时回归。
10. **用户拒绝后没有版本规则。** 用户拒绝应生成新 requirement 或 cheat pattern，并使旧候选保持拒绝记录；不能覆盖旧证据后重新命名为 pass。

### 5.3 建议的回路状态

```text
C0 → QA pass → 用户
 │
 ├─ evidence invalid → 重采集，不计实现轮次，但最多重采 1 次
 ├─ contract ambiguous → 回设计，新 revision
 ├─ implementation fail → C1
 └─ visual uncertain → 用户

C1 → 同上；实现 fail → C2
C2 → 仍 fail → NEEDS_USER_DECISION / redesign
```

---

## 6. 第 22 节七项决策建议

### 6.1 决策 1：项目专属 Design Skill + Visual QA Agent

**建议：同意，但附条件。**

- Design Skill 是项目规范与合同生成器，不应冒充审美模型。
- Visual QA 必须具有版本化协议、校准集、盲测结果和已知能力边界。
- QA Agent 应用全新隔离上下文审查，不读取实现者的自辩说明。

### 6.2 决策 2：采用 L0–L4 状态

**建议：同意。**

- 未经真实截帧 QA 的历史资产最多 L2，这一口径合理。
- L4 必须绑定具体 `contractRevision + buildHash + captureProfile`；任何改变视觉输出的重建都使签署过期，回到 L3，而不是永久 L4。

### 6.3 决策 3：四条基线 L4 前暂停批量新增类型

**建议：同意。**

允许的例外只有：

- S0 校准 mutants；
- 测试夹具；
- 为证明底座能力所需的最小诊断资产。

这些不得登记为新的正式内容类型。

### 6.4 决策 4：垂直切片和四基线顺序

**建议：同意，修订 S0。**

- S0 分成 S0a 校准与 S0b 火焰生产候选。
- 先冻结最小 Capture Profile 和参考角色，再写实现。
- 火焰内的 Light 只验证最小接口；完整真实光照质量仍留给基线 D，避免重复建设。

顺序“持续火焰 → 移动弹丸拖尾 → 模型附着 → 真实光效”合理。

### 6.5 决策 5：三条回路硬规则

**建议：有条件同意。**

- 改成最多 3 个候选 C0/C1/C2。
- 自动 Capture 必须带不可变来源与 hash 验证。
- uncertain 先分类，只有 `VISUAL_UNCERTAIN` 升级用户。
- 合同变化重开 revision，不计作普通修复。

### 6.6 决策 6：隔离评估 Coplay Unity MCP

**建议：同意，但排在 S1 以后。**

MCP 能提高 Unity 操作效率，不能解决设计质量。过早接入会扩大变量和污染面。S0 使用当前已验证的批处理、Editor 脚本和测试工具即可。

### 6.7 决策 7：VFX Graph 只做兼容 Spike

**建议：同意。**

Unity 2022.3 官方说明中，VFX Graph 对 URP 与 URP 移动平台的完整支持仍在开发；功能比较也显示部分能力受限。因此不应成为 S0 前提。[Unity 2022.3 VFX Graph](https://docs.unity3d.com/ja/2022.3/Manual/com.unity.visualeffectgraph.html)

Spike 至少验证：

- URP 14.0.12；
- Windows Player Build；
- 目标移动平台或图形 API；
- Lit/Unlit、Trail、Depth、Distortion 等实际所需功能；
- 编译器生成、Patch、事务回滚和批处理捕获。

---

## 7. 更好的整体方案

### 7.1 建议方案：四路证据的垂直生产切片

我不建议推翻 1.1，而是将它升级为“**四路证据包**”：

1. **Beauty Sequence**  
   正式相机连续画面，供视觉 QA 和用户查看。

2. **Diagnostic Render Passes**  
   effect mask、layer/object ID、depth/normal、receiver luminance 等机器诊断缓冲；只用于量测，不冒充最终画面。

3. **Semantic Telemetry**  
   状态、事件、粒子、Transform、Trail、Light、绑定对象和预算读回；证明不可见的内部事实。

4. **Human Verdict Corpus**  
   用户 pass/fail、原因、帧号和合同修订，形成项目自己的可检索黄金案例库。

### 7.2 与 1.1 的关键差异

| 方面 | 1.1 原方案 | 建议方案 |
|---|---|---|
| 像素门禁 | 主要从 Beauty RGB 反推行为 | Beauty 用于看，ID/Mask/Linear Pass 用于测 |
| 语义权威 | 组件、像素、QA 三层并列 | 每类要求明确一个权威证据，其余只交叉验证 |
| QA 校准 | S0 中实际跑并复盘，未规定数据集 | S0a 受控 mutants + 冻结盲测 + 指标门槛 |
| Required 要求 | 倾向全部映射截帧 | 先分 visual / behavioral / structural / budget |
| 迭代 | 最多 3 轮 | C0/C1/C2 不可变候选包 + 合同 revision 边界 |
| 经验积累 | 新作弊模式回写规则 | 额外保留用户签署/拒绝的黄金证据语料库 |

### 7.3 迁移成本

迁移成本为**中等**，不需要推翻文档主体：

1. 修改 7.9，加入 S0 必需字段和 requirement 分类。
2. 将 13.2b 改为 Beauty + Diagnostic Pass，并按本评审表降级指标。
3. 在 S0 前半加入 QA 校准夹具和盲测。
4. 为 Capture metadata 加 source hashes、capture profile 和 diagnostic pass manifest。
5. 修改 21 节完成定义，取消“所有 Required 都必须由截图判断”。

Unity 端新增成本主要是几种诊断材质/Renderer Feature 或受控替代 Shader，以及输出 Mask/ID/Linear buffer；相较于继续依靠 RGB 光流调阈值，这个成本更确定，也更容易解释失败原因。

### 7.4 最终建议

建议用户对 1.1 做出：

> **方向批准，实施暂缓；完成上述五项修订后升为 1.2-draft，再授权 S0。**

最需要优先修订的三项是：

1. S0 最小合同补 Capture Profile、参考角色、尺寸、seed、清理语义和 requirement 分类。
2. 13.2b 从普通 RGB 推断改为“组件权威 + Diagnostic Pass + Beauty 交叉验证”。
3. S0 拆成 QA 校准和真实火焰候选两个阶段，避免用同一个案例训练又证明自己。

