# VFX Composer 开发执行计划

> 文档状态：v1.3，S1–S11 已执行；M8 于 2026-08-22 通过独立验收
> 定位：本文档规定**实际开发顺序和每步的具体内容**，是 [PROJECT_PLAN.md](PROJECT_PLAN.md) 中 WP0–WP8 的执行版重排。范围、验收标准、门禁仍以原三份文档为准；两者冲突时，先按本文档执行顺序走，范围问题回到原文档裁决。
> 角色分工：**本文档是唯一的执行依据，开发全部由执行者（开发者本人 + Codex 等编码工具）完成**；计划维护者只负责本文档的清晰、严谨和随实际进展更新，不参与写代码。因此每个阶段的任务描述必须做到"执行者不需要额外口头解释即可开工"，发现描述不够开工的，视为计划缺陷，先回来改计划再开工。
> 核心原则：**按不确定性排序，不按依赖顺序排序。先花时间买信息（验证最可能失败的环节），再花时间买质量（把确定能做的事做精致）。**

---

## 0. 阶段总览

| 阶段 | 名称 | 参考工时 | 性质 | 对应原计划 |
|---|---|---:|---|---|
| S1 | 技术贯通 Spike | 3–5 天 | 抛弃式验证 | 新增（WP-1） |
| S2 | AI 环节小样 | 1–2 天 | 抛弃式验证 | 新增（WP-2） |
| S3 | 决策定版 + 工程基线 | 2–3 天 | 正式开发 | WP0 + 部分评审决策 |
| S4 | 领域模型与验证器 | 4–6 天 | 正式开发 | WP1 |
| S5 | 正式 2D 模板库 | 5–8 天 | 正式开发 | WP2 + DS3/DS4 |
| S6 | 正式编译器 | 6–9 天 | 正式开发 | WP3 |
| S7 | 运行时控制器与预览 | 4–6 天 | 正式开发 | WP4 |
| S8 | Patch 与局部重建 | 4–7 天 | 正式开发 | WP5 |
| S9 | Codex 正式工作流 | 2–4 天 | 正式开发 | WP6 |
| S10 | 3D 扩展 | 7–10 天 | 正式开发 | WP7 |
| S11 | 稳定化与发布 | 5–8 天 | 正式开发 | WP8 |

关键关系：

- S1、S2 是**闸门**：任一失败，项目在损失一周以内的成本时止损或转向，不进入 S3。
- S1、S2 的代码**必须丢弃**，只保留结论纪要（见 1.6、2.5）。
- S3 起进入正式开发，之后大体回归原计划顺序，但每阶段的开工条件和注意事项以本文档为准。
- S5 和 S6 存在交叠：编译器只需要 3 个模板就能开工，不必等 6 个模板全部定版（见 5.4）。

---

## 1. S1：技术贯通 Spike（3–5 天）

### 1.1 目的

用最脏、最快的方式回答项目的三个命门问题：

1. **Q1**：Unity Editor API 能否可靠地"拷贝模板 → 改粒子参数 → 组装 Prefab → 存盘"？
2. **Q2**：重复构建能否做到结构幂等（同输入 → 资产无变化）？
3. **Q3**：生成 Prefab 与模板的关系，应采用**深拷贝**还是 **Nested Prefab 引用**？

这三个问题的答案决定 S4–S6 全部基础设施的形状。在会议室里猜不出来，只能实测。

### 1.2 环境准备 ✅ 已完成（2026-08-22）

实际环境（已核实）：

- Editor：2022.3.62f3c1（`E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`）。
- Spike 项目：`D:\WorkWork\Assist\image_to_smart\spike\image_to_smart\`，2D URP 模板（URP 14.0.12，含 2D feature 与 Particle System 模块）。
- Spike 结束后 `spike\` 目录整体可删；正式工程（S3）另建目录。

Spike 期间的约束：不建 UPM 包，不配 asmdef，不写测试。所有代码放 `Assets/Editor/Spike/` 一个目录。

### 1.3 制作粗糙模板（第 1 天）

在 `Assets/SpikeTemplates/` 手工做 3 个模板 Prefab，丑没关系，但结构要真实：

| 模板 | 内容 | 目的 |
|---|---|---|
| `T_Core` | 一个 SpriteRenderer（Unity 内置圆形 Sprite + 橙色材质即可） | 验证 Sprite 类模板 |
| `T_Embers` | 一个 ParticleSystem（小点、低速率、短生命周期） | 验证粒子参数绑定 |
| `T_Burst` | 一个 ParticleSystem（Burst 发射、径向扩散） | 验证 Burst 类参数 + 多粒子系统组装 |

注意：

- 每个模板给一个**独立的材质文件**（不要用共享默认材质），因为后面要验证"材质实例生成"。
- 粒子用 Built-in Particle System，不碰 VFX Graph。

### 1.4 硬编码编译脚本（第 2–3 天）

写一个 EditorWindow 或 MenuItem，逻辑如下（怎么脏怎么来，但每一步是真实的）：

```csharp
// 伪代码，示意结构
var recipe = new Dictionary<string, object> {
    ["core"]   = new { template = "T_Core",   scale = 1.2f },
    ["embers"] = new { template = "T_Embers", rate = 18f, lifetime = 0.55f },
    ["burst"]  = new { template = "T_Burst",  count = 24, speed = 3.5f },
};
// 1. 组装策略 A（深拷贝）和 B（引用）各写一版
// 2. 读字典 → 找模板 → 按策略组装 → 改参数 → 挂到根节点下
// 3. PrefabUtility.SaveAsPrefabAsset 存到 Assets/Generated/
```

必须验证的具体技术点（这是 Spike 的核心产出，逐项打勾）：

- [ ] **粒子参数修改**：`ParticleSystem` 的模块（`main`、`emission` 等）是 struct 属性访问器，`var main = ps.main; main.startLifetime = x;` 这样改在 Editor 下能否正确写入 Prefab 并持久化？（预期能，但要实测存盘后重新打开的值）
- [ ] **Emission Rate**：`emission.rateOverTime = new ParticleSystem.MinMaxCurve(18f)` 存盘验证。
- [ ] **Burst**：`emission.SetBursts(...)` 存盘验证。
- [ ] **材质实例**：为生成物创建材质副本（`new Material(templateMat)` + `AssetDatabase.CreateAsset`），改颜色，验证生成 Prefab 引用的是副本而非模板材质。
- [ ] **组装策略 A（深拷贝）**：`PrefabUtility.InstantiatePrefab` 模板 → `PrefabUtility.UnpackPrefabInstance`（完全解包）→ 改参数 → 挂到根 → `SaveAsPrefabAsset`。观察：生成物是否完全独立于模板？文件体积？
- [ ] **组装策略 B（引用/Nested）**：`InstantiatePrefab` 后**不解包**，直接改参数形成 override → 挂根存盘。观察：override 在生成 Prefab 里怎么序列化？模板改动会不会串到生成物？
- [ ] **幂等验证**：同一字典连续构建两次，用 `git diff`（把 Spike 项目临时 git init）对比 `Assets/Generated/` 下的 .prefab/.mat 文件。两次构建**字节级**是否一致？如果字节不一致，差异是什么（fileID？顺序？浮点格式）？这直接决定 S6 的 Build Hash 应该在哪一层做比较。
- [ ] **PlayMode 播放**：生成 Prefab 拖进场景，进 PlayMode，粒子正常播放，无 Editor 依赖报错。
- [ ] **重建的 GUID/引用稳定性**：把生成 Prefab 拖进一个场景 → 修改字典参数 → 重新构建（模拟"临时构建→覆盖"流程）→ 场景里的引用是否仍然有效？生成 Prefab 的 .meta GUID 是否保持不变？（结论决定 S6 的原子替换必须做成"就地更新内容、保留 .meta/GUID"，还是可以删旧建新。引用断裂 = 用户每次重建后场景全部失联，属于不可接受行为。）
- [ ] **粒子随机种子**：`useAutoRandomSeed = false; randomSeed = 42` 写入并持久化验证。（Recipe 的 `randomSeed` 字段依赖此机制才有意义；若不可靠，S4 需重新定义该字段语义。）

### 1.5 幂等与策略结论（第 3–4 天）

- 分别对策略 A、B 跑幂等测试，记录哪种方案的重复构建更稳定、diff 更干净。
- 测一次"模板被修改后重新构建"：改 `T_Embers` 的默认颜色，重建，观察两种策略下生成物的行为差异。
- **做出决策**：正式版采用 A 还是 B（预判是 A 深拷贝，但以实测为准）。

### 1.6 退出条件与产出

**退出条件（全部满足才进 S2）：**

1. 硬编码字典能生成可在 PlayMode 播放的组合 Prefab。
2. 三类参数（float 数值、Burst、材质颜色）都验证了写入与持久化。
3. 幂等测试有明确结论：在什么比较层级上（字节 / 序列化结构 / 参数值）可以做到"同输入无变化"。
4. 组装策略 A/B 已实测并选定其一，理由成文。

**产出：填写 `docs/spike-notes/S1_SPIKE_REPORT.md`**（模板已就位，逐项填写实测结果、API 坑、组装策略决策及理由、幂等比较层级结论）。**Spike 项目本身归档或删除，代码不得复制进正式仓库。**

**失败处理**：若 Q1/Q2 有硬性障碍（如参数写入不可靠、幂等在任何层级都做不到），暂停，回到 PROJECT_PLAN 第 19 节评估转向，不进入后续阶段。

### 1.7 注意事项

- ⚠️ 最大的纪律风险：舍不得扔 Spike 代码。**它的使命是产出那页纪要，不是产出代码。**
- ⚠️ 不要在 Spike 里顺手做 Validator、Schema、UI——发现自己在"把它做好"就是跑偏信号。
- ⚠️ 所有资产操作后记得 `AssetDatabase.SaveAssets()`，否则"看起来改了、重启丢失"会污染你的幂等结论。
- ⚠️ 批量资产操作用 `AssetDatabase.StartAssetEditing()/StopAssetEditing()` 包裹的话，注意 try/finally，异常不恢复会卡死资产库——正式版必须处理，Spike 里先记录这个坑。

---

## 2. S2：AI 环节小样（1–2 天）

### 2.1 目的

回答第四个命门问题：**Q4：LLM 能否稳定生成合法 Recipe，并根据错误反馈自我修正？** 同时用真实 AI 输出**反推** Recipe Schema 的形状——比先设计 Schema 再要求 AI 遵守更准。

### 2.2 准备（半天）

- 把 S1 的硬编码字典整理成一份 JSON 样例（手写即可）。
- 写一份给 AI 看的说明文档（Markdown，一页）：字段含义、可用模板列表（就 S1 那 3 个）、每个模板的参数名和范围。这就是未来 Template Manifest 的雏形。

### 2.3 实验（1 天）

用 Codex / Claude 做以下回合，**你人工扮演 Validator**：

1. "生成一个标准火球的 Recipe" → 检查输出 JSON 是否合法、字段是否臆造。
2. "一个更大、更慢、火星更多的火球" → 检查参数是否落在范围内、语义映射是否合理。
3. 故意给它一个含错误的 Recipe + 你手写的错误报告（模仿未来 Validator 输出格式，如 `E102: stages/travel/modules/embers/parameters/rate 值 250 超出范围 [0,100]`）→ 检查能否精确修复而不动其他字段。
4. "火星减少一半" → 检查能否输出**局部 Patch** 而不是重写整个 Recipe。

每一轮记录：成功/失败、AI 臆造了什么字段、什么样的错误信息格式让它修得最准。

### 2.4 用产出校准数据模型

根据实验记录回答：

- Recipe 字段哪些是 AI 天然理解的，哪些总被写错（写错的要么改名、要么在 Schema 说明里加注释）。
- 错误报告应该包含什么（实测结论大概率是：错误码 + 精确路径 + 当前值 + 允许范围 + 一句人话）。
- Patch 格式用什么形态 AI 最不容易出错。

### 2.5 退出条件与产出

**退出条件**：4 个回合中，AI 在 ≤2 次修正内产出合法结果的比例 ≥ 3/4。（达不到就要重新设计 Schema 表达或说明文档，而不是硬闯 S3。）

**产出**：填写 `docs/spike-notes/S2_AI_REPORT.md`（模板已就位）—— 实验记录、字段命名修正清单、错误报告格式结论、Patch 格式结论。实验用的 JSON（`spike-notes/sample-recipe.json`）和说明文档（`spike-notes/ai-readme-draft.md`）作为 S4 的输入草稿保留在同目录。

---

## 3. S3：决策定版 + 工程基线（2–3 天）

从这里开始是正式开发，代码全部新写。

### 3.1 决策定版（半天）

S1/S2 已经用实测回答了大部分"待评审决策"。现在一次性把 DEVELOPMENT_PLAN 第 21 节、DESIGN_PLAN 第 21 节的所有开放问题**写下结论**，存为 `docs/DECISIONS.md`。没有实测依据的项，按以下默认值定（都选保守项，日后可升级）：

| 决策 | 默认结论 |
|---|---|
| Unity 版本 | **已定版：2022.3.62f3c1**（Editor 路径 `E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe`）。原文档中"Unity 6 LTS"基线相应调整为 2022.3 LTS，URP 14.x |
| 首个目标平台 | PC 编辑器预览，保留 `mobile_medium` profile 字段但不做真机验收 |
| 组装策略 | 按 S1 实测结论（预期：深拷贝） |
| 生成物依赖 Runtime 包 | 接受 |
| Managed 资产 Inspector 手改 | 允许但重建覆盖，首版不做 Detach |
| Detach/Bake | P2，首版不做 |
| 2D 拖尾 | 首版只做一种（TrailRenderer 或 Particle Trail，按 S1 顺手程度选），模板层封装，日后可加另一种 |
| 自动截图 | 首版不做，只要编辑器预览 |
| JSON Schema 验证 | **不引入通用 JSON Schema 验证库**（Newtonsoft.Json.Schema 是付费授权）。结构+语义验证全部 C# 手写；`.schema.json` 文件仅作为给 AI 阅读的文档存在 |
| 工具 UI | 首版最小按钮面板（导入/Validate/Dry Run/Build/Preview + 报告文本区），三栏窗口推迟 |
| 3D 是否在 MVP | 在，但作为最后里程碑，2D 闭环全部通过后才开工 |

### 3.2 工程基线（2 天）

**仓库布局（定版）**：`D:\WorkWork\Assist\image_to_smart\` 即 git 仓库根，结构：

```text
image_to_smart/
├─ docs/                  # 全部计划与纪要文档（随代码一起版本化）
├─ project/               # 正式 Unity 项目（S3 创建）
├─ spike/                 # Spike 临时项目，整体列入 .gitignore，S2 结束后可删
└─ .gitignore             # Unity 官方模板（作用于 project/）+ 忽略 spike/
```

建议现在（S1 开始前）就对仓库根执行 `git init` 并提交 docs——计划文档本身的演进也应可追溯。

任务清单：

- 在 `project/` 创建正式 Unity URP 项目（2022.3.62f3c1，2D URP 模板，与 Spike 同构）；git 仓库按上述布局（**`.meta` 必须提交**；项目设置确认 Force Text 序列化、Visible Meta Files）。
- 创建 UPM 包 `Packages/com.vfxcomposer.unity/`，目录结构照 DEVELOPMENT_PLAN 第 8 节。
- 配三个 asmdef：`VFXComposer.Runtime`（不引用任何 Editor）、`VFXComposer.Editor`（引用 Runtime）、`VFXComposer.Tests.EditMode` / `.PlayMode`。
- 引入 `com.unity.nuget.newtonsoft-json`。
- 写一个冒烟测试（如"包能加载、一个恒真断言"），确认 Test Runner 跑通。
- **命令行测试脚本**（Codex 开发闭环的关键，没有它每轮改动都要人工开编辑器验证）：仓库根写 `tools/run-tests.bat`，内容形如：

  ```bat
  "E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe" -batchmode ^
    -projectPath "%~dp0..\project" -runTests -testPlatform EditMode ^
    -testResults "%~dp0..\test-results.xml" -logFile "%~dp0..\unity-test.log"
  ```

  同样写一个 `tools/compile-check.bat`（`-batchmode -quit -logFile`，仅验证编译）。验收标准：Codex 在不打开 Unity 图形界面的情况下，能通过脚本获得编译结果和测试结果。
  ⚠️ 已知限制：Unity 同一项目同时只能被一个编辑器实例打开——编辑器开着该项目时 batchmode 会因项目锁失败。工作约定：Codex 批量开发时关闭编辑器用脚本验证，人工视觉检查时再打开编辑器。
- 建目录约定：`Assets/VFX/Templates/`（只读输入）、`Assets/VFX/Generated/`（工具唯一写入区）。

### 3.3 退出条件

- 空包编译通过，EditMode/PlayMode 各有至少一个测试跑通。
- Runtime asmdef 中引用 `UnityEditor` 会编译失败（亲手验证一次）。
- `DECISIONS.md` 提交，所有开放决策关闭。

### 3.4 注意事项

- ⚠️ asmdef 引用方向此刻就要卡死。Runtime → Editor 的反向依赖一旦混入，后期拆除代价极大。
- ⚠️ 版本锁定写进 `README` 和 `ProjectSettings`，团队/多机开发时 Unity Hub 极易用错版本静默升级项目。

---

## 4. S4：领域模型与验证器（4–6 天）

### 4.1 开发内容

以 S2 校准过的 JSON 草稿为输入，实现（全部在 Editor asmdef，纯 C#，不碰 Unity 资产 API）：

1. **Recipe Model**：`recipeVersion / id / dimension / archetype / targetProfile / randomSeed / stages / metadata`；Stage：`id / trigger / duration / modules / enabled`；Module：`id / kind / templateId / parameters / attachTo / enabled`。用 Newtonsoft 反序列化，未知字段策略：**报错拒绝**（不是忽略——AI 臆造字段必须被抓住，这是 S2 的直接教训）。
2. **Template Manifest Model**：照 DEVELOPMENT_PLAN 第 6 节字段。
3. **Catalog**：扫描 Templates 目录下的 manifest 文件，建 `templateId → Manifest` 索引，检测 ID 冲突、GUID/路径不一致。
4. **验证器**（两层，同一报告格式输出）：
   - 结构验证：必填字段、类型、枚举值、ID 唯一性、`recipeVersion` 支持范围。
   - 语义验证：`templateId` 存在于 Catalog、模板 `kind`/`dimension` 与模块匹配、参数名在 Manifest 声明范围内、参数值在 min/max 内、`attachTo` 合法。
5. **验证报告格式**：`{ code, severity, path, message, actualValue, allowedRange }`——严格按 S2 结论设计，这个格式就是未来给 AI 的修复输入。
6. **规范化与 Build Hash**：Recipe 规范化序列化（键排序、数值稳定格式化如 `R` 或固定小数位、数组保序）→ SHA256。**浮点格式化是幂等的头号敌人**，这里写死规则并测试。
7. **预算模型**：按 profile 定义上限表（粒子数、材质数、Trail 数、时长等），输入 Recipe + Catalog 的 cost 数据，输出 error/warning/info。

### 4.2 测试（与实现同步写）

- 合法样例通过；每种非法样例（缺字段、未知字段、坏枚举、未知模板、参数越界、重复 ID）被拒且错误路径精确。
- Build Hash：语义相同但键顺序/空白不同的 JSON → 同 hash；任一语义字段变化 → 不同 hash。
- 预算：构造超限 Recipe → error 级结果。

### 4.3 退出条件

- 火球默认 Recipe（手写）+ ≥6 个非法样例全部行为正确。
- 全部测试为 EditMode 纯逻辑测试，不依赖任何真实模板资产（用手写的假 Manifest）。

### 4.4 注意事项

- ⚠️ **只为火球建模**。任何"未来可能需要"的字段（条件表达式、嵌套 Stage、继承）一律不加——这是 PROJECT_PLAN 风险表第一条"过度抽象"的落地执行。
- ⚠️ 错误信息现在就按"给 AI 读"的标准写。S9 时你会直接把验证报告粘给 Codex，模糊的报错等于 AI 修不动。
- ⚠️ `parameters` 用 `Dictionary<string, JToken>` 之类持有，类型收窄放在语义验证层做，别在反序列化层做强类型——模板参数类型是 Manifest 决定的，编译期不知道。

---

## 5. S5：正式 2D 模板库（5–8 天，与 S6 部分并行）

### 5.1 开发内容

按 DESIGN_PLAN 第 5–11 节执行设计流程（Brief 已有，直接从故事板/模块表开始），制作 6 个模板：

制作顺序（按 S6 的需求排序）：

1. `PFT_2D_FireCore`（Sprite 主体）
2. `PFT_2D_Embers`（粒子）
3. `PFT_2D_FireImpact`（Burst 粒子）
   —— **前 3 个完成即通知 S6 开工** ——
4. `PFT_2D_FireTrail`
5. `PFT_2D_LaunchFlash`
6. `PFT_2D_Shockwave`

每个模板的完成定义：

- Prefab 可独立拖进场景播放。
- Manifest 完整：参数带 min/default/max，且**三点实测**（min 仍可见、default 是标准表现、max 不崩不卡）。
- cost 字段填实测估算值。
- 通过 DESIGN_PLAN 第 9 节轮廓检查（黑/灰/亮背景、正常/缩小尺寸）。

### 5.2 视觉金样（DS4）

6 个模板齐后，人工在场景里摆一个"目标火球"（不经过编译器，手工组合），按 DESIGN_PLAN 第 15 节评审表打分，全维度 ≥1 且轮廓/节奏/命中 =2 才算金样通过。这个手工组合场景保留，作为后续编译器输出的对照物。

### 5.3 注意事项

- ⚠️ 没有技术美术时，把预算集中在 **Travel 主体 + Impact** 两处——这两处决定演示观感。LaunchFlash 和 Shockwave 可以朴素。
- ⚠️ 模板迭代没有下限，给自己定死时间盒：单模板超 1.5 天就先用当前版本入库，视觉打磨记入 S11。
- ⚠️ Manifest 的 binding 键此刻就用正式符号名（如 `particle.emission.rate_over_time`），和 S6 的 Handler 注册表共用一份常量定义，避免字符串漂移。
- ⚠️ 模板材质用 URP 自带 Particles/Unlit 起步，**不自写 Shader**——自写 Shader 是本阶段最大的时间黑洞。

### 5.4 与 S6 的并行

前 3 个模板入库后，S5 剩余模板与 S6 编译器并行推进（单人开发就交替进行：上午美术、下午代码，防止在单一维度上钻牛角尖）。

---

## 6. S6：正式编译器（6–9 天）

### 6.1 开发内容

按 S1 选定的组装策略，正式实现 DEVELOPMENT_PLAN 第 9 节流水线：

1. **Binding Handler 注册表**：`binding 符号键 → Action<GameObject, JToken>` 白名单映射。首版只实现模板实际用到的键（预计 8–12 个）。**严禁反射属性路径**。
2. **Build Planner / Dry Run**：对比 Recipe hash 与上次 Build Manifest，输出 creates/updates/unchanged/blocked 清单，**不动任何资产**；同时调用 S4 的预算模型，把 error/warning/info 并入 Dry Run 报告——**budget error 使构建按钮不可用**（blocked 项标明原因），warning 允许构建但必须显示。
3. **组装器**：临时目录构建 → 逐模块实例化模板 → 应用参数（经 Handler）→ 按 Stage 组装层级（照 DEVELOPMENT_PLAN 10.2 结构）→ 生成材质实例 → 挂 `GeneratedVfxController`（S7 提供，S6 期间先挂空壳组件占位）。
4. **构建后验证**：生成物引用扫描（无模板目录外的野引用、无丢失引用）、结构与 Dry Run 计划一致。
5. **原子替换**：临时目录构建成功 → 替换 Generated 下的 Managed 输出 → 写 Build Manifest（recipeId、revision、build hash、模板版本表、时间戳）。失败则清理临时目录，上次成功构建不动。
6. **最小 UI**：一个 EditorWindow：选 Recipe 文件 → Validate / Dry Run / Build 三个按钮 + 报告文本区。

### 6.2 测试

- EditMode：加载真实模板 → 构建 → 断言生成 Prefab 结构、参数值、材质引用。
- 幂等：同 Recipe 构建两次，第二次 Dry Run 全部 unchanged，资产文件无 diff（按 S1 确定的比较层级断言）。
- 失败回滚：构造中途抛异常的构建（如非法 binding），断言 Generated 目录与构建前一致。
- 金样：`fireball-2d.default` 的结构快照测试（遍历生成 Prefab 的层级/组件/关键参数，序列化成文本快照进 git，diff 即回归）。

### 6.3 退出条件（= 里程碑 M3）

手写默认 Recipe → 一键生成 2D 火球 Prefab，重复构建幂等，失败不破坏上次结果，结构与 S5 的手工金样场景肉眼一致。

### 6.4 注意事项

- ⚠️ 这是全项目最重的阶段。如果 S1 做得扎实，这里应该没有"原理性意外"，只有工作量；一旦出现原理性意外（某参数写不进去、幂等破了），停下来补一个微型 Spike，不要在正式代码里边猜边试。
- ⚠️ 写入路径硬校验：任何写操作前断言目标路径在 `Assets/VFX/Generated/` 下，测试里专门验证"试图写 Templates 目录会被拒绝"（验收场景 A5）。
- ⚠️ 结构快照测试是幂等和回归的主防线，优先级高于一切 UI 打磨。

---

## 7. S7：运行时控制器与预览（4–6 天）

### 7.1 开发内容

1. **`GeneratedVfxController`**（Runtime asmdef）：
   - 公开接口照 DEVELOPMENT_PLAN 10.3：`PlayLaunch / StartTravel / SetTravelTransform / PlayImpact / StopEffect`。
   - 内部按 Stage 根节点激活/停止子对象；粒子用 `Play()/Stop(withChildren, StopEmittingAndClear)` 控制。
   - `StopEffect(immediate:true)` 必须清干净粒子和 Trail（`Clear()` + Trail 处理）；`immediate:false` 停止发射等自然消亡。
   - 提供 `ResetForPool()`：对象池取出前恢复初始状态。
2. **编译器接线**：S6 的空壳占位换成真实组件，构建时把各 Stage 根节点引用写入 Controller 序列化字段。
3. **固定预览场景**（Editor 侧）：中性灰背景、正交相机、地面参照线、起点/终点标记；预览窗口按钮：Launch Only / Travel Loop / Impact Only / Full Sequence / Reset。Full Sequence 就是"Launch → 沿直线移动到终点 → Impact"的简单驱动脚本。

### 7.2 测试

- PlayMode：各阶段独立播放断言（粒子系统 isPlaying 状态、激活层级正确）。
- Full Sequence 顺序断言。
- Stop 后无残留粒子（`particleCount == 0`）。
- Reset 后可重复播放。
- 程序集测试：Runtime.asmdef 引用列表里无 Editor（编译期已保证，测试留档）。

### 7.3 退出条件（= 里程碑 M4）

生成火球在预览场景中完整演示 Launch→Travel→Impact；断开一切工具代码（临时禁用 Editor 目录）后 PlayMode 仍可播放（验收场景 A7 的简化版）。

### 7.4 注意事项

- ⚠️ Trail 的清理是经典坑：TrailRenderer 在对象瞬移（对象池复用/Reset）时会拉出一条横跨屏幕的轨迹，`Clear()` 的调用时机必须在位置设置之后。测试里专门覆盖。
- ⚠️ Travel 是循环阶段，粒子 Simulation Space 用 World 时注意停止后残留粒子归属；这类表现细节记录到模板 Manifest 的已知限制里。

---

## 8. S8：Patch 与局部重建（4–7 天）

### 8.1 开发内容

1. **Patch 模型**：`op ∈ {replace, add, remove, enable, disable}`，路径用稳定 ID：`/stages/{stageId}/modules/{moduleId}/parameters/{param}`。格式细节按 S2 实测结论。
2. **Patch 验证**：目标 revision 匹配、路径存在、op 合法（remove 仅限非必需模块）、参数范围复用 S4 验证器。
3. **应用与版本**：验证通过 → 生成新 Recipe（revision+1）→ 追加变更记录（`docs` 或 Recipe 旁的 `.history.json`）。
4. **影响分析与局部构建**：对比新旧 Recipe，标出受影响模块 → Build Planner 只对受影响部分输出 updates，其余 unchanged → 编译器按计划局部更新。（若 S1 结论显示局部更新实现成本高，可接受"全量重建但报告精确标注受影响模块"作为首版——幂等保证了全量重建无副作用，**局部构建是优化不是正确性需求**，这一点想清楚可以省 2–3 天。）

### 8.2 测试

- "火星 rate 减半" Patch → 只有 embers 相关资产/参数变化，结构快照 diff 仅限该模块。
- revision 不匹配 → 拒绝。
- 路径不存在 / 越界值 → 拒绝且报告精确。
- Patch 后 Recipe 仍通过全量验证。

### 8.3 退出条件（= 里程碑 M5）

验收场景 A6 完整通过。

---

## 9. S9：Codex 正式工作流（2–4 天）

### 9.1 开发内容

（S2 已验证可行性，此阶段是把小样固化成正式流程。）

1. **AI 工作区约定文档**：`docs/ai-workflow/` 下放置：Recipe 编写规范（含 `.schema.json` 描述文件）、模板目录说明（从 Manifest 自动导出的参数表）、错误报告解读说明、Patch 编写规范。
2. **参数表导出工具**：Editor 菜单一键从 Catalog 导出 Markdown 模板参数表——AI 读的文档必须机器生成，手维护必然过期。
3. **流程验证**：完整走 5 遍 "文字需求 → AI 写 Recipe → 工具 Validate → 报告回贴 → AI 修复 → Build"，和 3 遍 "文字增量需求 → AI 写 Patch → 应用"。记录成功率。
4. CLI/BatchMode 入口（`validate-recipe` / `build-recipe`）**仅在**手工贴报告的往返变得烦人时才做，否则推迟到 S11 之后。MCP 明确不做。

### 9.2 退出条件（= 里程碑 M6）

新的自然语言需求（不是调试过的那句），AI 在 ≤2 轮修复内产出可构建 Recipe，成功率 ≥ 4/5。

---

## 10. S10：3D 扩展（7–10 天）

### 10.1 开工门禁（= Gate D）

2D 闭环（S4–S9）全部通过、Recipe v1 在 2D 全程未发现需要破坏性修改的字段缺陷。**若发现了，先出 v2 迁移再开工 3D。**

### 10.2 开发内容

按 DEVELOPMENT_PLAN 第 11 节 + DESIGN_PLAN 第 14 节：

1. 3D 模板组（Sphere Mesh 核心 + Billboard 火焰、3D TrailRenderer、Billboard 火星、Camera-facing Impact、Ring Mesh Shockwave），流程同 S5。
2. Catalog/编译器扩展：模板 `dimension` 过滤、3D 专用 binding Handler、3D Sorting/RenderQueue 规则。
3. 3D 预览模式：透视相机 + 可切换视角（正面/侧面/斜上/近/远）。
4. `fireball-3d.default` Recipe：**复用 2D Recipe 的 Stage/Module 结构，仅替换 templateId 和参数**——能做到这一点本身就是 M7 的验证目标；做不到的地方逐条记入差异报告。

### 10.3 退出条件（= 里程碑 M7）

同一套 Launch/Travel/Impact 语义生成 3D 火球；五视角评审通过；2D/3D 差异报告成文；Unsupported 能力显式报告而非静默忽略（验收场景 A8）。

### 10.4 注意事项

- ⚠️ 这一阶段的目的是**验证抽象**，不是做出华丽的 3D 火球。视觉标准可以低于 2D 金样，语义复用度才是验收核心。
- ⚠️ 出现"为了 3D 往 Recipe 加 2D 用不到的字段"时警惕——少量可以（如 bounds），成片出现说明抽象有问题，触发 PROJECT_PLAN 风险表"2D 设计无法扩展 3D"的应对。

---

## 11. S11：稳定化与发布（5–8 天）

- 全量回归：金样快照、幂等、回滚、Patch、A1–A8 验收场景逐条跑一遍并记录。
- 性能预检报告落地（明确标注"静态预检，非真机认证"）。
- 错误信息全量走查：每个错误码有精确路径 + 人话说明。
- 文档：安装说明、Recipe/Manifest 规范定稿、模板制作规范、AI 工作流说明、MVP 验收报告。
- 打版本 `0.1.0`（2D 闭环可先标 `0.2.0` 的策略照 PROJECT_PLAN 第 18 节）。
- 视觉打磨时间盒：把 S5 欠下的打磨在固定预算内做，不追加。

---

## 12. 全局注意事项（贯穿所有阶段）

### 12.1 纪律类

1. **Spike 代码不进正式库**（S1/S2 唯一产出是纪要）。
2. **只为火球建模**：任何字段/机制若首个案例用不到，一律不写。第二个技能原型（Impact/Slash）才是泛化的时机。
3. **范围变更走 PROJECT_PLAN 第 13 节流程**：新想法先记 `docs/CHANGELOG_REQUESTS.md`，默认进 P2，不打断当前阶段。
4. **每阶段结束提交一次阶段纪要**（半页即可：完成项、偏差、新风险、下阶段是否 Go）。
5. 单人开发的节奏保护：连续两天卡在同一个问题上 → 写下问题、换一个工作包推进、次日再回来。卡点本身也是项目信息。

### 12.2 技术坑清单（提前知道，别现场踩）

| 坑 | 影响阶段 | 对策 |
|---|---|---|
| Newtonsoft.Json.Schema 付费授权 | S4 | 不引入，验证全手写（已定版） |
| ParticleSystem 模块是 struct 访问器，改完必须存盘验证 | S1/S6 | Spike 实测 + 每个 Handler 带持久化测试 |
| 浮点序列化格式不稳定破坏 Build Hash | S4/S6 | 规范化序列化写死数值格式并单测 |
| Prefab 保存的 fileID/顺序非确定性 | S1/S6 | 幂等比较放在结构层而非字节层（按 S1 实测结论定层级） |
| `StartAssetEditing` 异常未配对导致资产库卡死 | S6 | try/finally 强制配对，代码评审必查 |
| TrailRenderer 瞬移拉线 | S7 | Reset 流程先设位置后 `Clear()`，测试覆盖 |
| Runtime→Editor 反向依赖混入 | 全程 | asmdef 卡死 + S7 断开工具代码播放测试 |
| 模板目录被构建误写 | S6/S8 | 写路径白名单断言 + A5 专项测试 |
| Unity 补丁版本漂移 | 全程 | 版本写进 README，多机开发核对 |
| AI 臆造字段被静默忽略 | S4/S9 | 反序列化未知字段 = 报错 |
| 重建"删旧建新"导致 GUID 变化、场景引用断裂 | S6/S8 | 原子替换必须就地更新内容并保留 .meta/GUID；S1 清单第 10 项实测 |
| batchmode 与编辑器实例争抢项目锁 | S3 起全程 | 工作约定：脚本验证时关编辑器；脚本失败先查锁再查代码 |

### 12.3 何时停下来重新评审

出现以下任一信号，暂停当前阶段，对照 PROJECT_PLAN 第 19 节做 Go/No-Go：

- S1/S2 退出条件达不到。
- S6 期间出现原理性意外且微型 Spike 也解不开。
- Recipe 为支持火球单案例已需要 ≥3 次破坏性改版。
- 任一阶段实际耗时超过参考上限的 2 倍。

---

## 13. 与原文档的对照关系

| 本文档 | 原计划 | 变化 |
|---|---|---|
| S1/S2 | 无 | 新增：风险前置验证 |
| S3 | WP0 + 各"待评审决策" | 决策集中定版；JSON Schema 库结论新增 |
| S4 | WP1 | 增加"未知字段报错"、AI 导向的错误格式 |
| S5 | WP2 + DS3/DS4 | 明确制作顺序、时间盒、与 S6 并行点 |
| S6 | WP3 | 组装策略由 S1 实测预定；结构快照为主防线 |
| S7 | WP4 | 无实质变化 |
| S8 | WP5 | 明确"局部构建是优化不是正确性需求"的降级路径 |
| S9 | WP6 | CLI 降为按需项；MCP 明确移出 |
| S10 | WP7 | 验收重心明确为语义复用度而非画面 |
| S11 | WP8 | 无实质变化 |

原文档的 Gate A–E、验收场景 A1–A8、质量指标、缺陷等级、DoR/DoD 全部继续有效，本文档不重复。

---

## 14. 变更记录

| 版本 | 日期 | 变更内容 |
|---|---|---|
| v1.0 | 2026-08-22 | 初版：S1–S11 执行顺序与内容 |
| v1.1 | 2026-08-22 | Unity 版本定版 2022.3.62f3c1；环境准备标记完成并记录实测路径；明确角色分工（计划/执行分离）；新增 S1/S2 纪要模板 |
| v1.2 | 2026-08-22 | 自检修订：S1 清单新增第 10 项（重建 GUID/引用稳定性）与第 11 项（粒子随机种子持久化）；S3 定版仓库布局（image_to_smart 为仓库根，正式项目在 project/）并新增命令行测试/编译脚本任务（Codex 无 GUI 反馈闭环）；S6 Dry Run 接入预算门禁；坑清单补 GUID 断裂与 batchmode 项目锁两条；三份原文档头部加基线勘定说明 |
