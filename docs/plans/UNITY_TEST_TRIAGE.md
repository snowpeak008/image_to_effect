# Unity EditMode 既有失败 triage 报告（O4）

> 状态：DELIVERED（返工轮完成，EditMode 0 失败）　|　日期：2026-08-29　|　执行者：O4 开发子 agent（分支 `task/O4b-unity-test-triage`）
>
> 对象：`docs/plans/BASELINE_REPORT.md` 第 3.4 节记录的 Unity EditMode 8 个确定性既有失败。目标是逐项定位根因并二分处置：陈旧同步类直接修复，真实缺陷或需产品决策类出裁决请求。
>
> **两轮交付**：首轮（§1–§5）定位九个失败面、修复 2 项、提出 5 条裁决请求；返工轮（§6）落实主 agent 对全部裁决的批准，EditMode 归零。首轮各节保留原文作为根因证据，处置结论以 §6 为准。

## 1. 环境与复现

| 项 | 值 |
|---|---|
| 提交基点 | `master` @ `d0151d61` |
| 执行位置 | 独立 worktree `D:\wt\i2s-o4b`（分支 `task/O4b-unity-test-triage`） |
| Unity | 2022.3.62f3c1（`-batchmode -nographics`） |
| 命令 | `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-Unity.ps1 -Mode EditMode -TimeoutSeconds 2400` |

复现结果（`Library` 冷启动，墙钟 11 min 11 s，测试时长 493.7 s）：

```
total=657 passed=596 failed=8 skipped=53
```

**总数与 O3 基线完全一致（657 = 596/8/53）**，但失败集合与 O3 清单有 1 项差异，且该差异本身是重要发现：

| 失败用例 | O3 环境 | 本环境首轮 | 本环境复跑 | 说明 |
|---|---|---|---|---|
| `W17W18NextCandidatePreviewTests.W17Preview_…` | 失败 | 失败 | **通过** | 同一根因，是否触发取决于场景内联脚本存根能否被 Unity 复原：**偶发失败**（§3.2） |
| `W17W18NextCandidatePreviewTests.W18Preview_…` | 失败 | 通过 | 通过 | 同上，同一根因 |
| `W24S6WorkerBrokerSessionTests.TestOnlyBrokerHostCompletesFourReadsAndContentMismatchBeforeRevoke` | 通过 | **失败** | 失败 | 非代码缺陷：该 gate 要求先构建 .NET 侧 Release HandleProbe（§3.7） |

即：跨环境实际存在 **9 个已知失败面**，其中 W17/W18 两项共享一个根因且表现为偶发（三种组合都观测到：O3 两项皆失败、本环境首轮仅 W17 失败、本环境复跑两项皆通过）、HandleProbe 一项是环境前置条件。任务卡的"8 项"在本环境表现为上表组合。**W17/W18 这项不能当作"已自愈"——它是被证实的真实缺陷，只是失败不确定（§3.2）。**

## 2. 处置矩阵

| # | 失败用例 | 症状 | 根因分类 | 首轮处置 | 最终状态 |
|---|---|---|---|---|---|
| 1 | `S11ReleaseAcceptanceTests.ErrorCodeAudit_…` | 源码错误码集与文档清单不等价 | 陈旧清单（文档漏登记 14 码） | **已修**：补登 `E1930`–`E1943`（`149f30ef`） | 通过；越界已追认 |
| 2 | `W17W18NextCandidatePreviewTests.W17Preview_…` | preview 场景内 driver 组件为空序列（偶发） | **真实缺陷**（Runtime 源码：一文件两个 MonoBehaviour，driver 无脚本资产） | 裁决请求 R-1（附补丁方案；根因已实测确证） | **已修**（`7e5dcfd4`），重建/重开两条路径均决定性通过 |
| 3 | `W17W18NextCandidatePreviewTests.W18Preview_…` | 同上 | 同 #2 | 同 #2 | 同 #2 |
| 4 | `W24S3BaselineContractTests.CaptureToolBundle_…` | `W24RealLightingModule.cs`/`VfxDesignContract.cs` sha256 与 pin 不符 | **需产品决策**（封存证据链有意 fail-closed） | 裁决请求 R-2 | **已重封**（`3d5ce862`，7 文件） |
| 5 | `W24S6LocalReadOnlyFilesystemAdapterTests.DirectoryTarget_…` | 目录目标返回 `W24FS107`，期望 `W24FS109` | 陈旧断言（期望码在当前实现下不可达） | **已修**：改断言并加强为"真实 open 尝试 + 非常规谓词"双重校验（`b0903a96`） | 通过 |
| 6 | `W24S6WorkerHandleAdmissionTests.SessionRevocation…NoNativeHandle` | 不透明面暴露 `SafeFileHandle` 签名 | **真实缺陷**（Editor 源码实例方法签名违反契约） | 裁决请求 R-3（附补丁方案） | **已修**（`5519688f`），零行为改动 |
| 7 | `W24SustainedFlameProductionTests.CaptureToolBundle_…` | `VfxDesignContract.cs` sha256 与 pin 不符 | **需产品决策**（同 #4，且下游 111 文件受牵连） | 裁决请求 R-4 | **显式豁免**（`d76e29d4`，`[Ignore]` 计入跳过） |
| 8 | `W24WorkflowTests.StatusRegistry_…` | 生成物清单中存在非 L2 条目 | **需产品决策**（注册表扫描契约 vs 已入库的分组目录布局） | 裁决请求 R-5 | **已定版**（`e4cd5158`，容器闭集 + 新负向测试） |
| 9 | `W24S6WorkerBrokerSessionTests.TestOnlyBrokerHost…` | `HandleProbe.exe` 不存在 | 环境前置条件 | 说明前置条件（§3.7） | 通过（worktree 内已构建 Release HandleProbe） |

首轮：已修 2 项、裁决请求 5 项、环境说明 1 项。返工轮：全部落地，**EditMode 0 失败**（详见 §6）。

## 3. 逐项 triage

### 3.1 错误码清单不同步（已修）

**症状**：`CollectionAssert.AreEquivalent(sourceCodes, documentedCodes)` 失败。测试用 `\b[EI]\d{3,4}\b` 扫描包内 `Editor/`+`Runtime/` 全部 `.cs`，与 `docs/release/ERROR_CODES.md` 的表格行双向比对。

**根因**：离线复算（脚本逻辑与测试一致）得出单向差集——源码 196 码，文档 182 码，文档缺 `E1930`–`E1943` 共 14 码，反向差集为空。这 14 码由 `Editor/Elements/ElementNextCandidatePlan.cs` 与 `Editor/Elements/ElementNextCandidateCompiler.cs`（W3–W8 元素 next-candidate 编译器）发出，从未进入错误码契约文档。属典型的"清单漏登记"。

**处置**：在 `docs/release/ERROR_CODES.md` 追加一节 `## Element next-candidate compiler (W3–W8)`，按现有表格四列格式登记 14 行（稳定路径与含义逐条取自发出点）。复算结果：源码 196 / 文档 196，双向差集为空。

**范围说明（需主 agent 确认）**：被审计的"清单"实体位于 `docs/release/ERROR_CODES.md`，不在任务卡 allow-list 的路径前缀（`project/Packages/com.vfxcomposer.unity/**`）内；包内不存在等价清单，唯一的替代"修复方向"是从生产代码删除 14 个错误码，显然不成立。故按任务卡列举的"清单"语义执行修复，并**单独成提交**（`149f30ef`），如主 agent 认为越界可单独回退。

### 3.2 preview 场景缺 driver 组件（裁决请求 R-1）

**症状**：`roots.SelectMany(v => v.GetComponentsInChildren<W17W18NextCandidatePreviewDriver>(true)).Single()` 抛 `Sequence contains no elements`。

**排查过程**（均在本 worktree 实测）：

1. 场景文件不入库，由测试自身经 `W17W18NextCandidateAuthoring.BuildW17ForBatch()` 生成；删除场景与增量标记后由测试重建，仍失败——排除"陈旧场景资产"假设。
2. 临时诊断（枚举场景根对象及其组件类型名）输出：`W17NextCandidatePreviewDriver[Transform,<null>]`——driver GameObject 存在，其 MonoBehaviour 在重新打开后是 **missing script**（null 组件）。
3. 场景 YAML：driver 的 `m_Script` 指向场景内**内联 MonoScript 存根**（`m_ClassName: W17W18NextCandidatePreviewDriver`、`m_AssemblyName: VFXComposer.Runtime`），而同场景 12 个 cell 组件均以正常 `guid` 引用脚本资产。
4. 手工把内联存根块前移到引用它的 MonoBehaviour 之前再复跑：仍失败——排除序列化顺序假设。
5. 决定性诊断（`MonoScript.FromMonoBehaviour` + `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`）：
   - `W17W18NextCandidateCell` → `Packages/com.vfxcomposer.unity/Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs`，`7ba92fddc2a0f858b5f8174201c6e03d:11500000`；
   - `W17W18NextCandidatePreviewDriver` → 资产路径为空，GUID 全零，**没有任何脚本资产**。

6. 修复后的全量复跑中该用例通过（场景重建后内联存根这次被复原成功）——**这不是自愈，而是同一缺陷的另一种表现**：只要 driver 没有脚本资产，场景里存的就永远是内联存根，能否复原不由本仓库控制。

**根因**：`Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs` 在同一文件内声明了两个 `MonoBehaviour`（`W17W18NextCandidateCell` 与 `W17W18NextCandidatePreviewDriver`），且文件名与二者都不同名。Unity 的 `MonoImporter` 每个 `.cs` 只产出一个 `MonoScript`（此处绑定到文件中先声明的 `W17W18NextCandidateCell`），因此 driver 类型没有可序列化的脚本资产。把 driver 组件存进场景时 Unity 只能写内联存根，重新加载后该组件退化为 missing script。W18 场景在本环境偶然复原成功、O3 环境两侧都失败，正是这种"内联存根能否复原"的非确定性表现。

**影响**：不止测试失败——master 上由 authoring 生成的两个 next-candidate preview 场景，其回放 driver 实际上是坏组件（打开场景即 missing script），preview 的确定性回放/清屏间隔能力在场景里是失效的。这是真实缺陷，不是测试陈旧。

**建议方案（R-1）**：把 `W17W18NextCandidatePreviewDriver` 拆到与类同名的新文件 `Runtime/W17W18NextCandidate/W17W18NextCandidatePreviewDriver.cs`（纯搬移，零逻辑改动），使其获得 `guid:11500000` 脚本资产。`W17W18NextCandidateCell` 留在原文件、GUID 不变，已有引用不受影响；PlayMode 测试仅按类型引用，编译面不变。此举同时满足 `CODING_STANDARDS.md` §2.2"一个文件一个顶层类型"。预期同时归零 #2 与 #3。风险：需要重新生成两个 preview 场景（它们本就是测试生成物，不入库）。

### 3.3 W24FS107 ≠ W24FS109（已修）

**症状**：目录目标被拒时诊断码为 `W24FS107`，测试期望 `W24FS109`。

**根因**：`Editor/W24/S6/External/W24S6WindowsReadOnlyFile.cs` 的叶子打开使用 `FILE_NON_DIRECTORY_FILE`（外加 `FILE_OPEN_REPARSE_POINT`），内核在 `NtOpenFile` 阶段即拒绝目录，走 `OpenRelative` 的失败码 `W24FS107`；`W24FS109`（"必须是单链接非 reparse 常规文件"）只在句柄已被接纳、进入 `GetFileInformationByHandle` 元数据判定后才可能出现。也就是说在当前实现下，目录目标**不可能**产出 `W24FS109`，测试期望是陈旧断言。两条码都是 fail-closed 拒绝，安全语义无差别，且"内核层直接拒绝"比"接纳句柄后再判定"更严格。

**处置**：更新该用例断言，并按同文件既有先例（junction 用例：内核先拒 + 另行断言确定性谓词）加强为三重校验：

```
Assert.That(...Diagnostics.Single().Code, Is.EqualTo("W24FS107"), ...);
Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.EqualTo(1), ...);
Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0x10, 1, 0), Is.False, ...);
```

即仍然证明：①目标确实发起了一次真实 `NtOpenFile`（而非提前用路径猜测拒绝）；②拒绝码稳定；③生产谓词独立地把目录属性判为非常规文件。断言数量与强度均未下降。

**备选（若主 agent 认为应改实现而非测试）**：从叶子打开去掉 `FILE_NON_DIRECTORY_FILE`，让目录句柄被接纳后由元数据判定产出 `W24FS109`。代价是主动放宽内核层防线，并需回归 `W24FS107` 的其余用例（缺失文件、共享冲突）。本报告不采纳。

### 3.4 契约 sha256 pin 不符 ×2（裁决请求 R-2 / R-4）

**症状**：
- `W24S3BaselineContractTests.CaptureToolBundle_IsARealReproducibleIdentitySharedByAllThreeContracts`：`docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json` 中 `project/Packages/com.vfxcomposer.unity/Runtime/W24/W24RealLightingModule.cs` 的 pin 为 `25dfda6c…`，实际 `9208c260…`。
- `W24SustainedFlameProductionTests.CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet`：`docs/vfx-contracts/capture-tools/sustained-flame-capture-tool.bundle.json` 中 `project/Packages/com.vfxcomposer.unity/Editor/W24/S1/VfxDesignContract.cs` 的 pin 为 `536d17b0…`，实际 `c430ad1a…`。

**离线全量核对**（两个 bundle 共 30 余条 source pin）：**只有上述 2 条不符**，其余全部一致。行尾形态不是原因（LF/CRLF 两种编码下都不等于 pin 值）。

**根因**：两个源文件在本仓库历史中只有一次提交（baseline squash `038d1b0e`），此后从未改动；pin 值在仓库历史里也从未匹配过。即漂移发生在仓库历史之前（baseline 导入时已存在），被 pin 的旧内容**不可恢复**。而 `docs/stage-notes/W24_MEASURED_FAILURE_SEALING_PREP.md` 明确写着：bundle 的规范化哈希已冻结进三个 S3 capture profile，"此后改动任何生产源都会**故意**让 `VerifyToolBundle` 拒绝旧链"。因此当前失败是封存证据链按设计 fail-closed，而不是"pin 忘了更新"。

**为什么不按'让 pin 反映现状'直接修**：改 bundle 会改变其规范化哈希（已用与 `RecipeCanonicalizer` 等价的离线实现验证：当前 S3 bundle = `f605aacf…`、sustained flame bundle = `42954a33…`，与各契约中的 `captureToolHash` 完全一致）。连锁面：

| 重新封存对象 | 直接牵连 | 下游 |
|---|---|---|
| S3 bundle | 3 个契约的 `captureToolHash`（`w24_moving_projectile_trail` / `w24_weapon_socket_fragments` / `w24_real_light_receivers`）+ 3 篇 stage-notes/review 文档中记录的哈希 | 这 3 个契约的文件哈希目前未被任何文件 pin，连锁较浅 |
| sustained flame bundle | `sustained_flame_3d.contract.json` 的 `captureToolHash` | 该契约文件哈希（`4fed7e3e…`）被 **111 个文件** pin（candidate receipt/trace、evidence、manifest、calibration 快照） |

且 `docs/vfx-contracts/**`、`docs/vfx-candidates/**`、`artifacts/**` 均不在任务卡 allow-list 内。

**裁决请求 R-2（S3）/ R-4（S0b sustained flame）**，三个可选路径：

1. **明确豁免**：承认这两项是"pre-history 源漂移导致封存链失效"的既有事实，登记为已知失败并从 F2 的验收面剔除（代价：EditMode 无法全绿，需要豁免清单长期存在）。
2. **重新封存**：作为独立任务重算 bundle pin → bundle 哈希 → 契约 `captureToolHash` →（sustained flame 还需处理 111 个下游 pin），并在报告中说明这等于宣布旧的 C0/S0b 证据链作废、需要重新走 capture/seal 流程。**不建议由 triage 任务顺手做**，因为它改写的是 write-once 证据语义。
3. **回退源文件**：把两个源文件恢复到被 pin 的内容——**不可行**，该内容在仓库中不存在。

建议：S3 侧（R-2）连锁浅，若主 agent 希望 EditMode 全绿，可批准路径 2 的最小版本（4 个文件 + 文档记录）；sustained flame 侧（R-4）建议先走路径 1 豁免，避免触碰 111 个已封存证据 pin。

### 3.5 句柄暴露检查（裁决请求 R-3）

**症状**：`Assert.That(signature, Does.Not.Contain("SafeHandle"))` 失败，实际签名 `System.Boolean TryReadRelative Microsoft.Win32.SafeHandles.SafeFileHandle System.String System.Byte[]&`。

**根因**：测试枚举 `W24S6WorkerProjectHandleLease` 的全部实例方法/属性（含 non-public），要求签名里不出现 `IntPtr`/`SafeHandle`/`SafeFileHandle`。`Editor/W24/S6/Worker/W24S6WorkerHandleAdmission.cs` 中的私有实例辅助方法 `private bool TryReadRelative(SafeFileHandle rootHandle, string relativePath, out byte[] bytes)` 违反了这条契约。这是生产代码违反自身契约（测试断言正确、实现不合规），不是测试陈旧；因此按任务卡归入"真实缺陷，不自行修"。

**影响评估**：参数位出现句柄类型不等于把句柄泄给调用方（没有返回句柄的成员），实际风险等级低；但契约是"不透明面上不得出现原生句柄类型"，反射面确实不合规。

**建议方案（R-3）**：把根句柄选择从签名移进实现，行为完全不变：

```csharp
private enum LeaseRoot { Repository, ProjectRoot }

internal bool TryReadRepositoryRelative(string relativePath, out byte[] bytes)
    => TryReadRelative(LeaseRoot.Repository, relativePath, out bytes);

internal bool TryReadProjectRelative(string relativePath, out byte[] bytes)
    => TryReadRelative(LeaseRoot.ProjectRoot, relativePath, out bytes);

private bool TryReadRelative(LeaseRoot root, string relativePath, out byte[] bytes)
{
    // 锁内解析 root -> repositoryHandle / projectRootHandle，其余逻辑不动
}
```

（不要把该方法改成 `static` 来规避枚举——它依赖 `disposeGate`/`session`/`usable` 实例状态，且那样属于绕过契约而非满足契约。）

### 3.6 状态注册断言（裁决请求 R-5）

**症状**：`StatusRegistry_RegistersAllGeneratedEntriesAsProvisionalWithoutVisualClaim` 在 `first.Entries.All(entry => entry.Maturity == L2_VisualPlaceholder)` 处为 False。

**根因**：`W24StatusRegistry.ScanDirectories` 把 `Assets/VFX/Generated` 的**每个直接子目录**都当作一个 effectId，并要求存在 `ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json` 且其 `runtimeEntry`/`ownedOutputs` 能通过存在性、GUID、SHA-256 三重校验。但 `Generated` 下存在 3 个**分组容器目录**（其下再分 effect 子目录、清单为组内 `NextCandidateManifest.json`）：

| 目录 | 是否入库 | 结果 |
|---|---|---|
| `W11W13NextCandidate` | **已入库**（18 个文件） | 组名无 `<id>.manifest.json` → L0 |
| `W15NextCandidate` | 未入库（测试生成） | 同上 → L0 |
| `W17W18NextCandidate` | 未入库（测试生成） | 同上 → L0 |

离线复算（254 个 Generated 子目录）确认：**失败条目恰好且仅有这 3 个分组目录**，其余 251 个 effect 目录全部通过三重校验。由于 `W11W13NextCandidate` 是已入库内容，即使在全新干净检出上该测试也会失败——这解释了它的"既有、确定性"属性。

**为什么不直接修**：可选修法都不是"让 fixture 反映现状"：

1. 改注册表扫描契约（生产代码）：把"无自身清单但其后代目录持有清单"的目录识别为容器（跳过或递归到叶子）。需要定版容器判定规则，属产品/契约决策。
2. 改分组目录布局：把 next-candidate 组挪出 `Generated` 直接层级——会改动已入库生成物的路径与清单，牵连 ADR-007 写入面清单。
3. 改测试为扫描隔离 fixture：会削弱该用例"扫描真实存量而非手工维护计数"的既定意图（测试首行断言就是这条意图），属降低严格度，按任务卡禁止。

**建议方案（R-5）**：采纳路径 1，规则建议为"目录若不存在同名清单，但其子孙目录中存在被清单登记的 effect 目录，则视为容器，不登记自身、按叶子递归登记"，并补一条负向测试（容器目录下若存在无清单的叶子仍须 L0）。需主 agent 定版后另派任务实施。

### 3.7 附带发现（非 8 项，但影响 F2 验收面）

1. **EditMode 全量会污染工作区**：一次全量运行后 `git status` 出现 **257 个已跟踪文件被修改**（194 个 `ProjectSettings/VFXComposer/BuildManifests/*.manifest.json`、62 个 `Assets/VFX/**` 生成物、`ProjectSettings/EditorBuildSettings.asset`）与 227 个未跟踪新文件。抽查清单 diff 全部集中在 `dependencyHash` 字段——这是 Unity AssetDatabase 的导入产物哈希，随 `Library` 重建/机器环境变化，本质上不可移植却被写进了入库清单。**F2 若以 Unity 编译链路为验收面，必须先定版这些清单的可移植性策略**，否则每次跑测试都会产生大量噪音 diff，且"构建产物字节稳定"类断言会随环境漂移。
2. **`W24S6WorkerBrokerSessionTests` 需要 .NET 侧产物**：`services/VFXComposer.Broker.HandleProbe/bin/Release/net8.0/VFXComposer.Broker.HandleProbe.exe`。O3 的 worktree 里已跑过 `dotnet build -c Release` 故通过，本 worktree 未构建 .NET（任务卡明确不碰 .NET、不配置 feed）故失败。**结论：不是缺陷，是运行前置条件**；F2/后续跑 Unity gate 前应先 `dotnet build VFXComposer.sln -c Release`，或把该前置写进 `Invoke-Unity.ps1` 的使用说明。

## 4. 修复后验证

复跑命令与 §1 相同（`Library` 已缓存）：

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-Unity.ps1 -Mode EditMode -TimeoutSeconds 2400
```

| 轮次 | total | passed | failed | skipped | 测试时长 | 失败清单 |
|---|---|---|---|---|---|---|
| 修复前（复现 O3 基线） | 657 | 596 | 8 | 53 | 493.7 s | 见 §1 |
| 修复后 | 657 | **599** | **5** | 53 | 498.3 s | 见下 |

修复后剩余 5 项失败：

| 失败用例 | 归属 |
|---|---|
| `W24S3BaselineContractTests.CaptureToolBundle_IsARealReproducibleIdentitySharedByAllThreeContracts` | 裁决请求 R-2 |
| `W24S6WorkerHandleAdmissionTests.SessionRevocationInvalidatesLeaseAndOpaqueSurfaceExposesNoNativeHandle` | 裁决请求 R-3 |
| `W24SustainedFlameProductionTests.CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet` | 裁决请求 R-4 |
| `W24WorkflowTests.StatusRegistry_RegistersAllGeneratedEntriesAsProvisionalWithoutVisualClaim` | 裁决请求 R-5 |
| `W24S6WorkerBrokerSessionTests.TestOnlyBrokerHostCompletesFourReadsAndContentMismatchBeforeRevoke` | 环境前置条件（§3.7-2） |

另有 `W17W18NextCandidatePreviewTests` 两项本轮通过但属偶发失败（R-1），**不计入"已归零"**。

结论：**确定性修复 2 项（596→599 通过、8→5 失败）+ 豁免清单 5 项 + 偶发项 1 项（根因已确证）**。EditMode 未全绿，剩余项全部有根因与建议方案，按任务卡"修复 N 项 + 明确豁免清单"交付。

## 5. 裁决请求清单（首轮提出，全部已批准并落地——落地明细见 §6）

| 编号 | 对象 | 请求 | 越界面 | 建议 |
|---|---|---|---|---|
| R-1 | `Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs` | 批准把 driver 类拆分到同名新文件（纯搬移） | Runtime 生产代码 | 批准；可一并归零 2 个失败面，风险极低 |
| R-2 | `docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json` + 3 个 S3 契约 | 批准重新封存 S3 capture tool bundle，或明确豁免 | 契约/证据文件 | 二选一，连锁浅（约 4 文件 + 文档） |
| R-3 | `Editor/W24/S6/Worker/W24S6WorkerHandleAdmission.cs` | 批准用 `LeaseRoot` 枚举替换私有实例方法的句柄参数 | Editor 生产代码 | 批准；零行为改动 |
| R-4 | `docs/vfx-contracts/capture-tools/sustained-flame-capture-tool.bundle.json` + `sustained_flame_3d.contract.json` | 明确豁免，或立独立任务重新封存（牵连 111 个下游 pin） | 契约/证据文件 | 建议豁免 |
| R-5 | `Editor/W24/Workflow/W24StatusRegistry.cs` | 定版"分组容器目录"扫描规则后实施 | 生产代码 + 契约语义 | 定版后另派任务 |
| 附 | `docs/release/ERROR_CODES.md`（已改，提交 `149f30ef`） | 追认该文件的 allow-list 扩展 | 文档路径前缀越界 | 追认或单独回退 |

## 6. 返工轮：主 agent 裁决落地（2026-08-29）

主 agent 对首轮 5 条裁决请求 + 1 条越界追认全部按建议方向批准。落地明细如下。

### 6.1 越界追认

`docs/release/ERROR_CODES.md` 的 14 码补登（`149f30ef`）**保留原样**，allow-list 越界已追认，无新动作。

### 6.2 R-1：preview driver 独立脚本资产（已修）

- `Runtime/W17W18NextCandidate/W17W18NextCandidatePreviewDriver.cs`（新增）：`W17W18NextCandidatePreviewDriver` 类逐字搬移，零逻辑改动；随之生成 `.cs.meta`（新 GUID）一并入库。
- `Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs`：删除该类，并移除随之不再使用的 `using System.Linq;`；`W17W18PreviewFamily` 与 `W17W18NextCandidateCell` 留在原文件，**GUID 不变**，既有引用不受影响。
- 场景侧：两个 preview 场景是测试生成物（不入库），删除旧场景与增量标记后由 authoring 重建。重建后校验：两个场景的**内联 MonoScript 存根数 = 0、无 guid 的 `m_Script` 引用数 = 0**，driver 已按正常 `guid:11500000` 引用。
- 决定性验证：①全量轮（先删场景 → authoring 重建 → 打开）两项通过；②随后单独重跑该 fixture（不删场景，直接从磁盘重开——即首轮偶发失败的那条路径）两项再次通过。偶发性消除。

### 6.3 R-2：S3 侧最小重封（已执行）

重封理由与影响面见提交 `3d5ce862` 的信息。实际改动 **7 个文件**（比首轮估算的 4 个多 3 个，原因是契约自哈希链）：

| 文件 | 改动 |
|---|---|
| `docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json` | 2 条 source pin 按真实文件字节刷新：`W24RealLightingModule.cs` `25dfda6c…`→`9208c260…`、`VfxDesignContract.cs` `536d17b0…`→`c430ad1a…` |
| `docs/vfx-contracts/w24_moving_projectile_trail.contract.json` | `captureProfile.captureToolHash` `f605aacf…`→`c300573c…`；`contractHash` `93fce950…`→`22035428…` |
| `docs/vfx-contracts/w24_weapon_socket_fragments.contract.json` | 同上；`contractHash` `88c752a1…`→`e011654d…` |
| `docs/vfx-contracts/w24_real_light_receivers.contract.json` | 同上；`contractHash` `0a7b6c67…`→`fb54bb65…` |
| `docs/vfx-traces/w24_moving_projectile_trail.implementation-trace.json` | `contractHash` 同步（`W24T003` 要求 trace 绑定精确契约哈希） |
| `docs/vfx-traces/w24_weapon_socket_fragments.implementation-trace.json` | 同上 |
| `docs/vfx-traces/w24_real_light_receivers.implementation-trace.json` | 同上 |

链条：刷新 source pin → bundle 规范化哈希 `f605aacf…`→`c300573c…`（用生产 `RecipeCanonicalizer` 与独立离线实现双算，结果一致）→ 3 个契约的 `captureToolHash` → 3 个契约的自哈希（`contractHash`，由生产 `VfxDesignContractJson.ComputeContractHash` 计算，验证器 `W24C004` 强制）→ 3 个 trace 的 `contractHash`。所有哈希值均由 Unity 内生产代码计算，未手算；文件为定点文本替换，无重排版。

未牵连：这 3 个契约的**文件哈希**未被任何 receipt/evidence/manifest pin（已全仓核对），因此没有 write-once 证据被改写。

遗留（**不在本次 allow-list 扩展内，留主 agent 决定**）：3 篇文档仍以叙述形式记录重封前的旧哈希 `f605aacf…`——`docs/stage-notes/W24_S3_REPORT.md`、`docs/stage-notes/W24_MEASURED_FAILURE_SEALING_PREP.md`、`docs/vfx-reviews/W24_S3_GRAPHICS_CAPTURE_PRODUCER_REPORT.md` 与 `docs/vfx-reviews/W24_S3_TYPED_DIAGNOSTIC_ENTITY_PREP.md`（后两篇同时记录旧契约自哈希）。它们是阶段历史记录、无测试断言；若要求"文档不留旧值"，需再开一个纯文档提交。

### 6.4 R-4：S0b 侧豁免落地（显式跳过）

`W24SustainedFlameProductionTests.CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet` 加 `[Ignore("R-4 exemption: …see docs/plans/UNITY_TEST_TRIAGE.md §3.4.")]`，并在方法上方写明豁免理由四要点（漂移早于仓库历史、被 pin 内容不可恢复、重封牵连 111 个下游 pin、主 agent 2026-08-29 豁免）与"不得削弱下方断言"的约束。**断言一条未删、容差一处未放宽**，跳过在 NUnit 结果里以 `Skipped:Ignored` 可见。

### 6.5 R-3：句柄面枚举化（已修）

`Editor/W24/S6/Worker/W24S6WorkerHandleAdmission.cs`：新增私有 `enum LeaseRoot { Repository, ProjectRoot }`，`TryReadRelative` 的首参由 `SafeFileHandle` 改为 `LeaseRoot`，句柄在 `disposeGate` 锁内解析（`root == LeaseRoot.Repository ? repositoryHandle : projectRootHandle`）。两个 `internal` 读取入口改为传枚举。行为零改动；未用"改 static 规避枚举"的绕过手法。

### 6.6 R-5：容器目录闭集定版（已落地）

`Editor/W24/Workflow/W24StatusRegistry.cs` 新增两个显式闭集（共三个名字，与裁决一致）：

| 闭集 | 成员 | 语义 |
|---|---|---|
| `EffectContainerDirectories` | `W11W13NextCandidate`、`W15NextCandidate` | 自身名字不是 effectId、自身免同名清单要求、**永不作为条目登记**；扫描下探恰好一层，每个子目录照常按 effectId 三重校验（存在性/GUID/SHA-256）。实测新增 30 个条目（20 + 10），全部 L2 通过 |
| `SeparatelyManifestedDirectories` | `W17W18NextCandidate` | 其产物由 `NextCandidateManifest.json` 方案自持（子目录是 `W17`/`W18`/`Shared` 分包层，本就不进 S0a BuildManifest 注册表），故声明并整体跳过、不下探；其校验由 `W17W18NextCandidate*` 测试负责 |

**闭集之外一切照旧 fail-closed**：任何未声明目录缺同名清单仍是 L0；容器**内部**的子目录缺清单同样是 L0（不因身处容器而豁免）。

> 与裁决字面的一处细化：裁决把三个名字放在同一个"容器闭集"里并要求"其下子目录照常按 effectId 规则校验"。实测 `W17W18NextCandidate` 的直接子目录是 `W17`/`W18`/`Shared` 三个分包层而非 effectId，下探一层必然产生 3 个 L0 条目，无法归零；故拆成"下探型容器"与"自持清单型容器"两个闭集，两者都是显式闭集、规则均为收紧。若主 agent 要求单一闭集，需另定"分包层"判定规则。

测试侧（`Tests/EditMode/W24WorkflowTests.cs`）：
- 原用例 `StatusRegistry_RegistersAllGeneratedEntriesAsProvisionalWithoutVisualClaim` 追加两条断言：任何已声明容器名都不得作为条目出现；`w11nc_ambient_dust_volume`、`w13nc_blade_tempest_ultimate_3d`（入库的容器子目录）必须出现在登记中。
- 新增 `StatusRegistry_DeclaredContainersAreNotEffectsAndEverythingElseStillFailsClosed`：用临时目录固化闭集内容、容器子目录登记、自持清单容器整体跳过、以及"容器内缺清单的子目录与未声明目录一律 L0"。

### 6.7 环境前置条件（#9）

按裁决在 worktree 内构建：

```
dotnet build services\VFXComposer.Broker.HandleProbe\VFXComposer.Broker.HandleProbe.csproj -c Release
```

结果 0 warning / 0 error（本地批准 feed 复制到 worktree 的 `.codex_tmp/`，`.codex_tmp/` 在 `.gitignore` 内、不入库）。`W24S6WorkerBrokerSessionTests.TestOnlyBrokerHostCompletesFourReadsAndContentMismatchBeforeRevoke` 随之通过。**.NET 工程零改动**（仅构建）。

### 6.8 返工轮最终 EditMode 数字

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-Unity.ps1 -Mode EditMode -TimeoutSeconds 2400
```

| 轮次 | total | 通过 | 失败 | 跳过 | 退出码 | 测试时长 |
|---|---|---|---|---|---|---|
| 首轮复现（O3 基线对照） | 657 | 596 | 8 | 53 | 3（结果闸） | 493.7 s |
| 首轮修复后 | 657 | 599 | 5 | 53 | 3（结果闸） | 498.3 s |
| **返工轮** | **658** | **604** | **0** | **54** | **0** | 514.6 s |

- `total` 由 657 增至 658：R-5 新增的 1 条负向测试。
- `skipped` 由 53 增至 54：**其中 1 条是 R-4 显式豁免**（`W24SustainedFlameProductionTests.CaptureToolBundle_BindsTheExactS0bAuthoritySourceSet`，`Skipped:Ignored`），其余 53 条与基线一致。
- `failed = 0`，脚本结果闸返回 exit code 0（`result=Skipped:Ignored`）。

补充验证：随后单独重跑 `W17W18NextCandidatePreviewTests`（从磁盘重开场景，不重建）→ 2/2 通过，确认 R-1 消除了偶发性。

## 7. 改动清单（两轮合计）

| 提交 | 文件 | 说明 |
|---|---|---|
| `b0903a96` | `project/…/Tests/EditMode/W24S6LocalReadOnlyFilesystemAdapterTests.cs` | 目录目标拒绝码断言对齐 + 两条加强断言 |
| `149f30ef` | `docs/release/ERROR_CODES.md` | 补登 `E1930`–`E1943` 共 14 个错误码 |
| `6141c6f9` | `docs/plans/UNITY_TEST_TRIAGE.md` | 首轮 triage 报告 |
| `5519688f` | `project/…/Editor/W24/S6/Worker/W24S6WorkerHandleAdmission.cs` | R-3 `LeaseRoot` 枚举替换句柄参数 |
| `d76e29d4` | `project/…/Tests/EditMode/W24SustainedFlameProductionTests.cs` | R-4 `[Ignore]` 显式豁免 |
| `e4cd5158` | `project/…/Editor/W24/Workflow/W24StatusRegistry.cs`、`project/…/Tests/EditMode/W24WorkflowTests.cs` | R-5 容器闭集定版 + 测试 |
| `3d5ce862` | `docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json`、3 × `docs/vfx-contracts/w24_*.contract.json`、3 × `docs/vfx-traces/w24_*.implementation-trace.json` | R-2 S3 重封（7 文件） |
| `7e5dcfd4` | `project/…/Runtime/W17W18NextCandidate/W17W18NextCandidatePreview.cs`、`…/W17W18NextCandidatePreviewDriver.cs`(+`.meta`) | R-1 driver 类拆分为独立脚本资产 |
| （本次） | `docs/plans/UNITY_TEST_TRIAGE.md` | 返工轮结论与最终数字 |

合计 15 个文件（Unity 包 6、契约/trace 7、`docs/release` 1、`docs/plans` 1）。**.NET 侧零文件改动**；未删除任何断言、未放宽任何容差；唯一的跳过是 R-4 的显式可见豁免。测试运行造成的 257 个 `dependencyHash` 漂移文件已 `git checkout -- project` 还原，未进入任何提交（该漂移本身仍是 F2 的前置议题，见 §3.7-1）。
