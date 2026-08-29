# 代码规范执行标准

> 适用范围：`docs/plans/OPTIMIZATION_MASTER_PLAN.md` 下派发的全部开发任务。子 agent 交付前必须自检本清单；审计子 agent 按本清单验收。

## 1. 构建与工具链（硬性）

1. .NET SDK 固定 `8.0.420`（`global.json`，rollForward 禁用），C# 12，`net8.0`。
2. `TreatWarningsAsErrors=true` 已全局开启：任何 warning 即验收失败。
3. NuGet 只允许本地批准 feed（`.codex_tmp/w24-phase1-approved-feed`），包锁定文件（`packages.lock.json`）必须随依赖变更一并更新；禁止私自添加外部 package source。
4. 新增工程必须加入 `VFXComposer.sln`，继承 `Directory.Build.props`/`Directory.Packages.props`（中央包管理），不得在 csproj 内写版本号。
5. Unity 包（`project/Packages/com.vfxcomposer.unity`）只能用 Unity 兼容 API 与 Newtonsoft.Json，禁止引入 net8 专有 DLL（ADR-003）。

## 2. C# 代码风格

1. `Nullable enable`、`ImplicitUsings enable`；新代码不得出现 `#nullable disable`。
2. 遵循所在工程的既有风格（命名、文件组织、一个文件一个顶层类型）；公共 API 使用 XML doc 注释说明"做什么与边界"，不写叙述性废话注释。
3. 禁止捕获后吞掉异常；错误必须映射为稳定错误码或类型化结果，与现有 `Protocol`/`Broker` 的稳定码风格一致。
4. 不引入未使用的依赖、死代码、面向"未来可能"的抽象层。

## 3. 安全与边界（继承 ADR-002/003/005/006，硬性）

1. 日志/异常/测试输出禁止出现：raw endpoint、Authorization/token、SecretRef 内容、prompt 原文、图片字节、未脱敏项目路径。统一走 redaction。
2. Desktop 不直接读写 Unity 项目文件；项目写入只允许经批准的构建路径，写入面为 ADR-007（ACCEPTED）定版的封闭双成员清单：`Assets/VFX/Generated/**` + `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json`；`Assets/VFX/Shared/**` 与单点之外的 `ProjectSettings/**` 构建期只读，越界即 fail-closed。
3. 保存配置、启动、页面导航必须零网络；网络请求只能由用户/任务的显式动作触发。
4. AI 通道无 fallback：一个通道一个显式绑定，失败即 fail-closed，不得静默换 route。
5. secret 一律经 DPAPI/SecretRef 存取，UI 只允许 entry-only。

## 4. 测试要求

1. 新增的每个行为类必须有对应单元测试：正常路径 + 至少一条拒绝/失败路径。
2. 涉及文件写入的功能必须有"越界路径被拒绝"的负向测试。
3. 涉及队列/生命周期的功能必须有取消与崩溃恢复（或等价模拟）测试。
4. 测试不得依赖真实外部服务、真实 secret、真实付费调用；网络测试用 loopback/mock。
5. 修改已有代码时，先跑受影响工程的既有测试，交付报告中附通过数。

## 5. Git 与交付纪律

1. 每个任务在独立分支 `task/<任务ID>-<slug>` 开发；大改动用 `D:\wt\` 下独立 worktree，避免污染主工作区。
2. 只改动任务卡 allow-list 内的文件；需要越界时停止并报告主 agent，不得自行扩权。
3. 提交信息格式：`<type>(<任务ID>): 摘要`，type ∈ feat/fix/docs/test/chore/refactor。
4. 不得直接提交/合并到 master；合并由主 agent 在验收 PASS 后执行。
5. 交付报告必须包含：改动文件清单、构建命令与结果、测试命令与通过数、自检清单勾选、已知限制。

## 6. 文档任务附加标准

1. 需求文档（PRD）必须含：目标与非目标、用户流程、功能需求（编号、可测试）、失败/边界行为、与现有代码/schema 的映射、验收场景。
2. ADR 必须含：背景、决策、备选方案、威胁模型影响、fail-closed 行为、不作声明清单。
3. 中文为主，代码标识符/路径保持原文；禁止复制大段代码进文档，用路径引用。

## 7. 验收流程

1. 开发子 agent 交付报告 → 主 agent 初审（allow-list、报告完整性）。
2. 审计子 agent 只读验收：复跑构建/测试、抽查代码规范、对照任务卡验收标准，输出 PASS / FAIL+问题清单。多个任务的验收可并行。
3. FAIL 允许原任务一次返工；返工后仍 FAIL 则任务关闭，由主 agent 重新拆解派发。
4. PASS 后主 agent 合并分支并更新主计划状态板。
