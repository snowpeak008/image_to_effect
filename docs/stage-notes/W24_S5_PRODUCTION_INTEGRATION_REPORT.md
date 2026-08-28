# W24 S5 恢复原计划扩展：生产集成与门禁报告

日期：2026-08-26
状态：当前直接源码快照已在隔离 shadow Unity 完成 focused 机器回归：S1 contract/Trace `15/15`、S5 production gate `26/26`、首次正式构建事务 `5/5`、typed binary canonical `4/4`、typed raw diagnostics `5/5`，批处理入口为 `7 passed / 1 intentionally ignored / 0 failed`。后续 test-only C1/C2 transaction 为 `10/10`，machine-failure invalid-only structure verifier 为 `20 passed / 1 permission-dependent ignored / 0 failed`；对应最终 Unity 进程均自然关闭且外层进程边界为 exit `0`。真实 machine verdict evaluator/issuer 仍未实现；所有视觉、QA、L3、L4、商用、Publication 与用户裁决结论仍保留给相应 authority/用户。
范围：仅恢复原计划扩展所需的合同优先准入、Catalog/UI 状态防误标、编译入口与测试；未批量重做视觉资产、未新建或替换正式视觉资产、未伪造 L3/L4。

## 1. 真实盘点结论

原计划 W-C0–W-C3 与 W1–W18 的源码和机器门禁已经覆盖了能力、风格、元素、Archetype、独立内容和组合编排；它们不等于已获得 W24 的合同/证据/用户签署闭环。

| 批次 | 已实现的真实能力或内容 | 机器状态 | 视觉状态（不得提升） |
|---|---|---|---|
| W-C0 | v1 兼容 `behavior`、对象式 `style`、能力注册、Sampler、能力/风格 Patch | 已完成 | 非视觉基础设施 |
| W-C1 | 12 弹道：linear/accel/parabola/homing/wave/boomerang/bounce/orbit/pierce/split/chain-hop/volley | 后续授权的下一候选已接通 split/chain/volley 多载体执行并通过隔离 Edit/Play/Preview 机器门 | 旧候选的**用户拒绝保持有效**；下一候选为 `VISUAL_PENDING`，不能重写为通过 |
| W-C2 | 8 射线：hitscan/sustained/sweep/charge/reflect/occlude/converge/arc-link | 后续授权的下一候选已接通多段、端点、遮挡/烧灼点、汇聚与跳线执行并通过隔离 Edit/Play/Preview 机器门 | 旧候选的**用户拒绝保持有效**；下一候选为 `VISUAL_PENDING` |
| W-C3 | 10 时序/范围：telegraph/delay/tick/charge/channel/chain/expand/implode/moving/growth；真实视觉槽与双出口 | 后续授权的 4×3 有界下一候选已完成机器门与 W-C1/W-C2 共享回归 | 旧候选的**用户拒绝保持有效**；下一候选为 `VISUAL_PENDING` |
| W1/W2 | 8 个初始 style token、共享 style 底座、Studio 五页签、3 个能力+皮肤示范 | 机器完成 | `VISUAL_PENDING` |
| W3–W8 | 47 默认元素 Recipe、4 风格变体、51 strict Runtime Entry；火/霜/雷/水风/岩自然毒/圣暗奥术 | 机器完成 | `VISUAL_PENDING` |
| W9/W10/W16 | 2D/3D 风格专项与第二风格包，共 32 strict Entry；总计 14 style token | 机器完成 | `VISUAL_PENDING` |
| W11/W12/W14/W17 | 环境、打击反馈、Screen/UI、游戏交互 UI，共 30 strict Entry | 机器完成 | `VISUAL_PENDING` |
| W15 | decal/weapon_trail/destruction/lifecycle/portal/loot，10 strict Entry | 机器完成 | `VISUAL_PENDING` |
| W13/W18 | 6 大招/Boss、8 角色组件、1 百鬼夜行和 4 套 showcase；19 strict Entry（其中 11 个依赖编排 Composite） | 机器完成 | `VISUAL_PENDING` |

来源是各批次的正式报告，尤其是 [W-C0–W-C3 报告](../stage-notes/WC0_WC3_CAPABILITY_REPORT.md)、[最终源码/视觉清单](../stage-notes/FINAL_SOURCE_DELIVERY_AND_VISUAL_ACCEPTANCE.md) 和 W1/W2、W3–W8、W9/W10/W16、W11/W12/W14/W17、W15、W13/W18 报告。

当前工作树的只读 catalog 输入统计为：296 份非 Patch Recipe JSON、296 个唯一 Recipe 路径、`0` 个 JSON 解析错误、`0` 个缺失 Recipe `.meta`、208 个正式 Generated 目录、220 份外置 Build Manifest。先前记录的 `209 Recipe / 221 Manifest` 及其当时 archetype 组合分布是历史盘点，不覆盖也不代表当前 catalog。style token 的已登记集合仍是 `stylized/cartoon/pixel/inkwash/semireal/holo/dark/neon/lowpoly/crystal/candy/cosmic/steampunk/ghost`；未带 style 的历史/专用 JSON 不被伪造归类。

原计划最终机器回归记录为：Compile exit 0；EditMode `224 total / 189 passed / 0 failed / 35 Explicit historical skipped`；PlayMode `44 total / 38 passed / 0 failed / 6 visual-capture Explicit skipped`，以及 Windows Player Build `1/1`。这是历史机器证据，不是本 S5 的视觉或 L4 结论。

## 2. 新的 contract-first production gate

`project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5ProductionGate.cs` 现直接调用 S1 的 `VfxDesignContractJson.ValidateJson`、`VfxImplementationTraceJson.ValidateJson` 与权威 Validators，不再维护一个缩减的合同/Trace JSON、语义或 hash 规则副本。

正式新建/更新入口必须在 Build 前提供并通过：

- 合同和 Trace 必须是 `docs/` 或 `ProjectSettings/` 下的持久化 UTF-8 文件；请求携带的是相对路径和文件字节 SHA-256，不再接受未绑定的 caller JSON；
- S1 权威 Schema / 语义 / trace 双向验证后，Trace 仍须精确绑定 DryRun 的 `buildHash` 与计划 Runtime Entry；
- `LEGACY` 不再由 `IsLegacy` 或 status 自报：只从现存、可解析、`legacy_audit` 且拥有同一 Runtime Entry 的权威 Build Manifest 导出，并且仅允许开发、永不发布；
- 公开请求已没有可构造的 S5 用户签名。当前没有宿主拥有的 opaque user-signoff issuer，因此持久化用户裁决记录只能作为证据，不能授权 L4 或 Publication；未来 issuer 必须绑定 effect、contract revision/hash、trace file hash、build/capture hash、evidence-corpus hash 与用户明确决定；
- `VISUAL_PENDING` 仍是默认状态，可继续开发但不可商业发布。用户视觉/L4 签署未在本次源码工作中完成。

### 2.1 Pre-C0 first-formal-build preregistration

为 S0b/S3 在尚无真实 Prefab GUID、Build/Capture identity 和 evidence 的阶段保留了一条**内部**窄通路：`EvaluateFirstFormalBuild(W24S5FirstFormalBuildRequest)`。它只接受 `Development + VISUAL_PENDING`，且合同必须为 `captureBindingStatus=PENDING_FIRST_FORMAL_BUILD`、Trace 必须为 `traceStatus=PENDING_FIRST_FORMAL_BUILD_BINDING`，并要求 scene/build、Trace build/capture/runtime GUID 都是精确的 `pending:formal-build` sentinel。

它仍读取 hash-pinned persisted contract/Trace，使用 S1 strict parsers，验证 effect/revision/contract hash、每个 requirement 的 authority、state/layer 双向映射、planned telemetry object、精确 Runtime Entry、contract authorizing runtime path、authoritative manifest reference 和安全 owned-output root。它禁止 authority/cross evidence 的提前声称，拒绝已有 Manifest、Publication、L3、L4、用户裁决和任意 root 替换。

成功后只产生一次性 opaque internal approval。只有 gate-owned 的 `CommitFirstFormalBuild(...)` 能消费它并事务性写入标记为 `PRE_C0_FIRST_FORMAL_BUILD` 的 `formalProduction` manifest binding；该事务从 approval 的不可变 provenance 重建 binding，重验 pinned contract/Trace、strict manifest、完整 ownedOutputs/文件 hash/.meta GUID，并在失败时恢复旧 manifest 与 owned outputs。调用方既不能构造 approval，也不能修改 binding；成功后才可通过 `TryGetBootstrapReceipt(...)` 取得不可变 bootstrap receipt。它不授予任何视觉等级、用户签署、capture 或发布权限。

bootstrap preregistration 文件为不可变 receipt，绝不在原路径重写其 hash-pinned pending identity。S0b/S3 候选冻结器必须以 receipt 为唯一来源，在 write-once 的 `docs/vfx-candidates/<effect>/C0/` 下另写 `design-contract.json`、`implementation-trace.json` 和 `candidate-receipt.json`：候选记录可填入真实 scene/build/manifest/GUID/capture identity，但 Trace 必须仍为 `C0_CAPTURE_PENDING` 且无 authority/cross evidence。候选并非当前 Manifest 的 formal binding，也不能通过普通 gate。证据齐备后须另写完整 Trace，状态为 `FORMAL_EVIDENCE_BOUND`，才可经常规 S5 exact-plan transaction 更新正式 Manifest；bootstrap manifest 在 Studio 中只能显示为 `PRE_C0 / VISUAL_PENDING`。

### 本轮生产门禁硬化

- 普通 `VfxProductionRules.EnforceAndWriteManifest(...)` 已不再暴露 `formalProduction` 参数，因此调用方无法向公开 writer 传入正式 authority。普通正式提交改为 S5 gate-owned writer：approval 和序列化 binding 均由私有 issuer 在提交时重建，且提交前再次读取 hash-pinned authority；
- C0 evidence seal 不会重写 C0，也绝不占用候选 C1。`FinalizeC0Evidence(...)` 仅接受哈希固定的 Capture metadata 与 artifact，复验 `candidate receipt → bootstrap Manifest bytes → 当前 ownedOutputs/.meta GUID → C0 Contract/Trace → capture metadata/artifact`，在 `docs/vfx-candidates/<effect>/C0/evidence/` write-once 生成 `FORMAL_EVIDENCE_BOUND` Trace 和 transition receipt。该 seal 显式绑定 `candidateRevision=0` 与 `evidenceRevision=1`；C0/C1/C2 始终只保留给不可变候选包。seal 只接受已通过且已捕获的 telemetry/diagnostic；`visualQa` / `user` 只能以 `Passed=false` 的 pending evidence 保留，不能由 capture 伪造通过。常规 S5 gate 复验同一链，而不再只相信 `traceStatus` 字符串；
- `W24S5RecorderCaptureCompletion` 提供四个明确的零参数 Editor batch/CI completion command：`FinalizeSustainedFlameC0Capture` 与三条 S3 command。正式 capture tool `Complete()` 后写入 sealed `diagnostics/machine-gate-trace.json`，再调用对应 command；command 只读 sealed inputs 并执行上述 C0 seal，不能也不会授予 L3/L4。
- C0 receipt 现在保存完整 `ownedOutputs[]` 快照。freezer 在创建候选前重新核验 output files 和 `.meta` GUID，拒绝任何在 bootstrap 与 freeze 之间的漂移；
- `docs/...` 的正式合同、Trace、裁决和证据 corpus 现在从**仓库根目录**解析；`ProjectSettings/...` 仍从 Unity 项目根目录解析。解析器拒绝绝对路径、反斜杠、空段、`.` 与 `..` 段，并在读入与提交前重验字节 SHA-256。
- `legacy_audit` 兼容不再只信任 manifest 字段：它逐项核对受管 Generated 根目录、Runtime Entry、全部 owned output 的文件存在性、原始 SHA-256、`.meta` GUID、以及 owned-output 清单与磁盘文件的精确集合。任一不一致均阻断，而不是降级成自报 legacy。
- 当前没有外部可信签名服务，因此产品代码对 L3、L4 与 Publication **fail closed**：仓库内的 QA、S0a、evidence corpus 或 user-verdict JSON 都只能作为证据，不能签发等级。未来只能由宿主拥有的交互式用户签署 API / opaque authority 签发 L4，并由同等 gate-owned 的 QA/S0a authority 签发 L3；本轮没有写入签名、token 或用户裁决。
- `Unchanged` 的正式构建在更新 rules manifest 前也重验批准计划；该 manifest 更新现在捕获旧字节，并在 validator 错误或写入异常时恢复，避免仅更新 formal/rules manifest 而没有可回滚事务。

`VISUAL_PENDING` 通过这些非视觉检查后可以进行开发 Build，但 Publication intent 被拒绝，永远不能得到 commercial/pass/production-ready 标记。L4 只能由上述已验证的内部裁决 authority 精确绑定同一 `contractRevision + contractHash + traceHash + buildHash + captureProfileHash + evidenceCorpusHash`；任一 binding 不一致即拒绝。旧 `legacy_audit` 条目仍可走兼容开发路径，但被明确标为 `LEGACY`，不可发布。

## 3. 最小集成点与兼容边界

- `VfxCompiler.DryRunProduction(...)`：先运行既有 v1 DryRun，再将其计算出的 build hash、Runtime Entry 和 Manifest 目标注入 S5 gate；门禁失败会使计划 blocked，尚未写入资产。
- `VfxCompiler.BuildProduction(...)`：提交 `DryRunProduction(...)` 附带的同一个批准 `VfxBuildPlan`，不会再调用 `Build(...)` 重算/替换计划；提交前与写入前均比较当前 recipe/canonical hash、revision、catalog dependency build hash、输出路径及持久化合同/Trace（和 L4 裁决，如有）字节 hash。
- 常规 `Evaluate(...)` 仅接受 `traceStatus=FORMAL_EVIDENCE_BOUND`；空值、`PENDING_FIRST_FORMAL_BUILD_BINDING` 和 `C0_CAPTURE_PENDING` 都明确拒绝，避免 bootstrap 或候选阶段权限重放为正式生产权限。
- 正式成功写入的 Build Manifest 记录嵌套 `formalProduction` binding：合同 path/file hash/contract hash/revision，Trace path/file hash，visual status，以及 L4 时的裁决/corpus binding；既有 Runtime Entry 与 ownedOutputs 仍由 production auditor 写入。该写入位于既有 Prefab/material/manifest 事务中，失败回滚旧资产和旧 manifest。
- Patch/AI authoring 可先调用同一个 `W24S5ProductionGate.Evaluate(...)`；它不接受任意 Unity 属性路径，也不直接写 Recipe、Patch、Prefab、Manifest 或 GUID。
- Studio Library 的新条目现在保守地投影为 `VISUAL_PENDING`，legacy 白名单投影为 `LEGACY`，且 `CommercialEligible=false`。Studio Review 写入的是 review evidence；不再显示或记录 `USER SIGNED`，不能以勾选框代替用户 L4。

S5 已接入 S1 的正式 lowerCamel readers；为区分候选与证据轮次，本轮仅给 Trace/freeze receipt 增加了 `candidateRevision` / `evidenceRevision` 绑定，未放宽 S1 parser/validator 的证据校验。

## 4. 自动化覆盖（dry-run / unit tests）

当前 `W24S5ProductionGateTests` 包含：

1. 缺持久化 contract、raw JSON/path/file-hash 不匹配和 `docs` traversal 拒绝；
2. 公开请求无法伪造 L3/L4；没有 C0/evidence/捕获证据链的 `VISUAL_PENDING` 也不能进入普通 formal build；
3. self-declared legacy、legacy manifest owned file SHA-256 / `.meta` GUID 篡改均不绕过门禁；
4. Trace 必须绑定 DryRun build hash/Runtime Entry，caller 伪造的空 BuildPlan 不能替代已批准 production plan；
5. manifest model 序列化包含合同/Trace/状态与 Runtime Entry/ownedOutputs binding，且 manifest snapshot restore 保留原字节；
6. isolated pending S3-shaped preregistration 仅在精确 pending identity、Development + VISUAL_PENDING、双向 layer mapping 与安全 owned root 下通过；Publication/L3/L4、非 pending GUID、缺 reverse mapping 和 path/root substitution 均被拒绝；
7. opaque approval 不能由非 gate issuer 构造，不能在 commit 前取得 receipt，pinned Trace 变更后不能消费；
8. 常规 S5 exact-plan gate 明确拒绝空值、`PENDING_FIRST_FORMAL_BUILD_BINDING` 和 `C0_CAPTURE_PENDING` Trace；`FORMAL_EVIDENCE_BOUND` 还必须具有可验证的 C0/evidence/capture artifact 链，防止状态字符串、首次构建或 C0 候选权限被重放为后续正式构建权限；
9. pinned preregistration Trace 在 approval 后发生变化时，bootstrap commit 被拒绝；公开 writer 的反射签名不含 formal authority，非 gate issuer 无法构造 approval；rollback 即使遇到 `InvalidDataException` 等非 IO 异常也会保留原事务错误并附加精确诊断。artifact path/hash drift、C1 replay、candidate/receipt/owned-output drift 的 Unity transaction 回归仍待补跑。

另更新 Studio 测试：即使自动和人工 review checkbox 全部勾选，输出仍为 `PENDING USER SIGN-OFF`，绝不写成用户已签署。

### 4.1 当前机器最终证据（r12/r13/r14）

六次最终运行均使用 `Unity 2022.3.62f3c1 (1623fc0bbb97)`，可执行文件为
`E:/workwork/steamgamework/unit/2022.3.62f3c1/Editor/Unity.exe`，参数边界为
`-batchmode -nographics -runTests -testPlatform EditMode`，项目边界为
`.codex_tmp/w24-fresh-20260825-0628/project`。每份日志都记录了精确 test filter、结果 XML
落盘、`ShutdownInProgress → Shutdown`、licensing channels 正常断开、`Cleanup mono` 和后续
shutdown telemetry；外层 Unity 进程记录均为 exit `0`。以下 SHA-256 与 `LastWriteTimeUtc` 均在
当前机器重新计算，XML 时间为 NUnit `test-run` 根节点的 UTC `start-time → end-time`：

- S1 `W24S1ContractAndTraceTests`（PID `29524`）：`15/15 passed / 0 failed / 0 skipped / 0 inconclusive`，
  XML `r12/s1-contract-trace-current.xml` 为 `2026-08-25 16:40:49Z → 16:40:50Z`
  （`duration=0.4424465`），完整路径
  `.codex_tmp/w24-stage-regression-results/r12/s1-contract-trace-current.xml`，
  `sha256:7a09ac96ccf291409351c24014eac6ecaf383789bebb68838b86379571fdc2b6`，
  `LastWriteTimeUtc=2026-08-25T16:40:50.0875516Z`；对应 log
  `sha256:33a0b27f9829f6a7377ace729cd7d16d67edc4287931e25d9b14cbf20bcfc1c4`，
  `LastWriteTimeUtc=2026-08-25T16:40:50.5008933Z`。
- S5 `W24S5ProductionGateTests`（PID `42064`）：`26/26 passed / 0 failed / 0 skipped / 0 inconclusive`，
  XML `r13/s5-production-gate-current.xml` 为 `2026-08-25 16:46:28Z → 16:46:29Z`
  （`duration=1.163651`），完整路径
  `.codex_tmp/w24-stage-regression-results/r13/s5-production-gate-current.xml`，
  `sha256:633b009138b4aa78a10ff6dfb6d5eb24d20d7b98e5a71b08c87bf5848426d052`，
  `LastWriteTimeUtc=2026-08-25T16:46:29.6723786Z`；对应 log
  `sha256:6028125cf3fc0599bfb2ba7e541b369667563c18424eb98b9c51363112220520`，
  `LastWriteTimeUtc=2026-08-25T16:46:30.1042038Z`。
- `W24FirstFormalBuildTransactionTests`（PID `35640`）：`5/5 passed / 0 failed / 0 skipped / 0 inconclusive`，
  XML `r13/first-formal-transaction-current.xml` 为 `2026-08-25 16:46:45Z → 16:46:45Z`
  （`duration=0.410367`），完整路径
  `.codex_tmp/w24-stage-regression-results/r13/first-formal-transaction-current.xml`，
  `sha256:8721b08ce23388832cdf1dcc8154b9009257c7c64ed3c2efd29b976dd50dfa36`，
  `LastWriteTimeUtc=2026-08-25T16:46:46.0284895Z`；对应 log
  `sha256:707a619f5449c904c31d2dc0647812ae55370d686977d211a5f3fbee39a43eda`，
  `LastWriteTimeUtc=2026-08-25T16:46:46.4697048Z`。
- `W24FormalBatchAuthoringEntrypointsTests`（PID `41400`）：`7 passed / 1 intentionally ignored /
  0 failed / 0 inconclusive`，XML 根结果因 ignored case 为 `Skipped:Ignored`，时间为
  `2026-08-25 16:47:03Z → 16:47:03Z`（`duration=0.0691739`），完整路径
  `.codex_tmp/w24-stage-regression-results/r13/formal-batch-entrypoints-current.xml`，
  `sha256:90bce856d89fd5fead42e56a4b86d7f70a56b4de4bed29199d57e5f2a715a6f1`，
  `LastWriteTimeUtc=2026-08-25T16:47:03.2767842Z`；对应 log
  `sha256:998265aa9a181f9bc610c6b9899b92ff2692f9dc3d7747f93cb90f853dbfeaed`，
  `LastWriteTimeUtc=2026-08-25T16:47:03.7606789Z`。唯一 ignored case 是
  `BatchEntryPoints_FailClosedInTheInteractiveEditor_BeforeAnyAuthoringCall`，XML 原因为
  `This guard is exercised only by the interactive Editor test runner.`；batch runner 不能伪装成交互式环境。
- `W24TypedBinaryCanonicalEncodingTests`（PID `44700`）：`4/4 passed / 0 failed / 0 skipped / 0 inconclusive`，
  XML `r13/typed-binary-canonical-current.xml` 为 `2026-08-25 16:47:39Z → 16:47:39Z`
  （`duration=0.0843814`），完整路径
  `.codex_tmp/w24-stage-regression-results/r13/typed-binary-canonical-current.xml`，
  `sha256:c7ffa865bd4dbe1896c508216e6dc96c3911f13af606cfb453941c2d0414fe7d`，
  `LastWriteTimeUtc=2026-08-25T16:47:39.4169808Z`；对应 log
  `sha256:8c68c1ebd2ba818767246f4ecf216d383b6f59f85c4c6a529100fb27818c8292`，
  `LastWriteTimeUtc=2026-08-25T16:47:39.7913040Z`。
- `W24TypedRawDiagnosticCaptureTests`（PID `43668`）：`5/5 passed / 0 failed / 0 skipped / 0 inconclusive`，
  最终 XML `r14/typed-raw-diagnostics-current.xml` 为 `2026-08-25 16:53:50Z → 16:53:50Z`
  （`duration=0.1339378`），完整路径
  `.codex_tmp/w24-stage-regression-results/r14/typed-raw-diagnostics-current.xml`，
  `sha256:02a60a90719a6031f8c7aa7b8f8265a298bce973ddf4d7df8549f40600f722bf`，
  `LastWriteTimeUtc=2026-08-25T16:53:50.8864756Z`；对应 log
  `sha256:a5d468eb4b4c165d442c4851b98ae5f000700aad292150d6b257b87931df61bc`，
  `LastWriteTimeUtc=2026-08-25T16:53:51.2705833Z`。

### 4.2 当前直接源码边界

对每个 suite 的直接实现源与该 suite 测试源，当前 package 与上述 shadow package 已逐文件比较；
下表源码相对路径均以 `project/Packages/com.vfxcomposer.unity/` 为根。
SHA-256 和 `LastWriteTimeUtc` 全部相同，且各组最晚源码写入均早于对应最终 XML 的
`start-time`。下表的集合 SHA-256 算法为：将该组相对路径按 ordinal 排序，逐行写入
`<lowercase-file-sha256><two spaces><relative-path>\n` 的 UTF-8 字节后再取 SHA-256；“最新边界”同时
给出该组最后写入文件的原始文件 SHA-256。

| suite 直接源集合 | 文件数 / 集合 SHA-256 | 最新源码边界（UTC；文件 SHA-256） |
|---|---|---|
| S1：strict JSON、Contract、Trace 与 S1 test | 4 / `af0b65f3a5614b032e9def8b262d530687b5211f08792cc82adc2980266a9029` | `Editor/W24/W24StrictJsonText.cs`；`2026-08-25T16:23:09.5969471Z`；`c9480177fce26b4b6aa55ba4b10fdc2352eeaef7a3a4bcfbcff752fe8dff20ec` |
| S5：strict/S1 readers、gate/evidence DAG/transition、compiler、production rules 与 S5 test | 9 / `8284a9b44074c094193378570f322d8f223ed7cd000c7361274156fe655e6e0a` | `Editor/W24/W24StrictJsonText.cs`；`2026-08-25T16:23:09.5969471Z`；`c9480177fce26b4b6aa55ba4b10fdc2352eeaef7a3a4bcfbcff752fe8dff20ec` |
| 首次正式构建事务：transaction、S0b/S3 authoring 与 transaction test | 4 / `b84356224a459b8f6e5bba19a4a8f2a69c1e3c92d9645c6f4b6a2ce78015a4f7` | `Editor/W24/S3/W24S3BaselineAuthoring.cs`；`2026-08-25T08:29:47.7900012Z`；`27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35` |
| batch：transaction、entrypoints、preview renderer infrastructure、S0b/S3 authoring 与 batch test | 6 / `fb91516b0511a750c9b626b018242ddb68cea1fb42ba94bb2d43734d4581366a` | `Editor/W24/S3/W24S3BaselineAuthoring.cs`；`2026-08-25T08:29:47.7900012Z`；`27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35` |
| typed binary：Contract、evidence DAG/transition、canonical encoder 与 typed-binary test | 5 / `f3d0592325a0399cad8474d0a3563043f22c5a1fc4d8fa04f215ff02ad14e6b7` | `Editor/W24/S1/VfxDesignContract.cs`；`2026-08-25T15:47:34.9377520Z`；`c430ad1a66946bbd75a6c98eb1032718039745020fea778e176a02ff5c750486` |
| typed raw：linear LDR/NPY/object-id-depth/renderer-mask diagnostics 与 typed-raw test | 5 / `39b8ebb3881ddfe68419b0380bad52c6f2613656e55aa1e85780e5d74fcc1580` | `Runtime/Diagnostics/W24RendererMaskDiagnosticCapture.cs`；`2026-08-25T03:58:16.5558301Z`；`a5dd283fabfbf46021ddb866b69ed8a155ceb969e9c52ec666aa310cdffbb247` |

r10/r11 的 S6 MCP envelope 尝试不属于上述六组 S1/S5 focused 最终证据，也不证明任何外部
adapter 已集成；其 orchestration timeout/retry 记录不作为本报告通过依据。r12 中较早的
`s5-production-gate-current` 虽已写出 `26/26` XML，但进程边界被 timeout 判为 rejected，已由
r13 的独立自然关闭运行取代；r12 的 S1 `15/15` 则是上列最终证据。类似地，typed raw 以更新且
自然关闭的 r14 为准，不引用 r13 的较早副本。

上述运行全为 `-nographics` focused EditMode tests，只证明各自覆盖的 strict contract/Trace、
production admission、事务回滚、batch-only API/identity、typed encoding 与 raw diagnostic policy；
不证明真实 graphics/capture 输出，不证明三条 S3 已实际生成 `FORMAL_EVIDENCE_BOUND` 记录，也不
证明外部 adapter、视觉 QA、L3、L4、商业可用、Publication authority 或用户裁决。

### 4.3 C1/C2 不可变候选事务（test-only focused verified；failure issuer pending）

状态：`C1_C2_TRANSACTION_INFRASTRUCTURE_UNITY_FOCUSED_VERIFIED / FAILURE_ISSUER_PENDING`。本节记录的是
2026-08-26 后续新增的源码、Roslyn 静态编译与独立 r21 Unity focused 运行；它不修改或追认
r12/r13/r14 的既有事实，也没有写入 formal evidence 或签发真实 C1/C2。

| 文件 | SHA-256 |
|---|---|
| `Editor/W24/S5/W24S5CandidateRevisionTransaction.cs` | `974479d8399cb0a9fd99284605ce44f9f0bbf0d8b2a5c873283d2a430d72b915` |
| `Editor/W24/S5/W24S5CandidateRevisionTransaction.cs.meta` | `e0eab2ea430dc81fbea16d2c8a78ffeda8ceed3980f71184ff6a1326f9e12ba7` |
| `Tests/EditMode/W24S5CandidateRevisionTransactionTests.cs` | `15f2f7096ad9da9281262a404f016a5dd89aa2ca71350028913a8823aeadfd48` |
| `Tests/EditMode/W24S5CandidateRevisionTransactionTests.cs.meta` | `efa09f827365798cad82654d23e28ba95f7b5abe35787fe41efb6f18bc949253` |

该独立 transaction primitive 只读 legacy `C0`，由 gate 从 hash-pinned predecessor 派生
`R<contractRevision>/C1`、`C2` 和 revision-owned asset root；每个候选 write-once 保存独立
Contract、pending Trace、receipt、Manifest/bundle/source snapshot 与 evidence root。提交前重放
predecessor/receipt、路径、schema、hash、ownedOutputs、Runtime Entry、Preview/camera（含深层 frozen-view binding）、bundle tool
version、Manifest reference、candidate-local source snapshot 与静态文件集合。最终 replay 在
repository-scoped `CreateNew` 排他锁内执行，锁保持到 `Directory.Move` 完成并在 `finally` 释放；
目标 parent chain 在创建后和最终 move 前再次拒绝 reparse point。`evidence/` 和
`terminal/` 是单独的 write-once 子树，既不构成静态文件漂移，也不能自行授予推进权。C2 后
fail-closed，不会导出 C3。

生产构建下普通 `Evaluate(request)` 始终拒绝，产品代码没有可构造的 failure authority。
`UNITY_INCLUDE_TESTS` 下的 opaque test issuer 仅用于机械事务夹具；其 receipt 强制标记
`TEST_ONLY_TRANSACTION_INFRASTRUCTURE`、`FAILURE_ISSUER_PENDING`、`VISUAL_PENDING` 和
`L2_MAXIMUM_PENDING`，并保持 QA/user record 为 null。当前源码的 focused tests 覆盖 test-only
C0→C1→C2、跳号/重放/C3 耗尽、漂移与原子回滚、terminal 子树、bundle/source snapshot 篡改拒绝、
锁竞争、最终写入锁生命周期与 move 前 parent-reparse 拒绝。Editor 与 EditMode test assembly
先通过现有 Bee dependency response files 完成 Roslyn 编译（两者 exit `0`），随后 r21 在隔离
shadow 中运行同一 focused filter：PID `14572`，外层 exit `0`，NUnit `10/10 passed`、无失败/跳过，
`2026-08-25 19:13:00Z → 19:13:01Z`，`duration=1.4629562`。XML SHA-256 为
`b0c29aad70a59654264eb83dd82e6202ddab805124136c8866b9f1b41881e81e`，log SHA-256 为
`adeba14e3825f22139f9531aeb3f11f5b485f723e6c5f577a40edaf95a8be5cc`；日志自然走到 licensing
disconnect 与 `Cleanup mono`。被 r21 取代的 r20 `9/10` 仅是测试期望旧错误文本，不作为最终证据。

真实 `MACHINE_FAIL` producer/receipt replay 尚未接入；`VISUAL_FAIL` 仍因独立 Visual-QA issuer
缺失而 fail-closed。Visual QA、用户裁决、L3、L4、商用、Publication 全部 pending。更完整的
布局、绑定与剩余门禁见 `W24_C1_C2_CANDIDATE_TRANSACTION_REPORT.md`。

### 4.4 Phase-B descriptor-structure replay（test-only focused verified；evaluator/terminal/authority pending）

历史文件名/类名 `W24S5MachineFailureProducer` 现只保留为兼容债务；唯一诚实入口已改为
`ReplayDescriptorStructure`。production 在纯内存 request-shape 校验后发现 registry 不存在，便于任何
candidate/descriptor/schema/raw/bundle 文件系统访问或 process/network/output 动作之前返回
`EVALUATOR_RUNTIME_PENDING`。test-only registry 只允许对 Phase-A legacy C0/E1 S0b descriptor 做两次
有界只读结构重放；成功态固定为 `TEST_ONLY_DESCRIPTOR_STRUCTURE_REPLAYED + EVALUATOR_PROVENANCE_PENDING`。
源码没有 publisher、terminal、machine-gate report、route receipt、verdict、transition 或可被 revision
transaction 消费的 authority surface。下表是 r27 的历史 Unity 绑定，不是后续 shared-raw 源码绑定：

| 文件 | SHA-256 |
|---|---|
| `Editor/W24/S5/W24S5MachineFailureProducer.cs` | `72c7d5d17236b64e6412efa5f1d9da529bd19b4a92b3668100d397022b1ad666` |
| `Editor/W24/S5/W24S5MachineFailureProducer.cs.meta` | `7bfa7b627bd03227fb2e27783733d98a8b5e44c9d07db5fe5564c5a7192852ec` |
| `Tests/EditMode/W24S5MachineFailureProducerTests.cs` | `d0fd0968e4cc88c8de5a9f7e616aedc52a068b31898ff19e9a00311e73e9efc2` |
| `Tests/EditMode/W24S5MachineFailureProducerTests.cs.meta` | `d5f90d27991b69e88da8508efcba31de86a4f4be2272890dd83f8243e12d0058` |

最终 source/tests 先经五个 Roslyn/.NET harness 编译为 0 error；其中独立 no-`UNITY_INCLUDE_TESTS`
production DLL SHA-256 为 `e7cffcd599e6df4c99d49d6b9bf11700c1fbaae8efe4b0d53ad034d7a6f42461`，smoke
输出 `EVALUATOR_RUNTIME_PENDING`。r27 在同一隔离 shadow 运行 focused filter：PID `42216`、外层
exit `0`；NUnit `12/12 passed`、无失败/跳过/未决，`2026-08-26 00:53:22Z → 00:53:30Z`，
`duration=8.0906665`。XML SHA-256 为 `7cae770320463285669fbb1e224f6baa05f3eec3baf05059c03d5a1b0e5a2faa`，
log SHA-256 为 `019e5d38c3523f386a452532abbd4d06b80375f3c2bf218ffbd5c0626c37eff4`；日志自然完成两路
licensing disconnect 与 `Cleanup mono`。退出后 canonical/shadow source/tests 仍逐字节一致，probe
candidate/raw/input、evidence lock 与 terminal 残留均为 0。独立终审为 scoped GO（P0=0、P1=0、
P2=2；P2 仅是未来 API denylist 与动态 zero-read 测试护栏不足）。

这只证明 r27 的只读 descriptor structure replay。后续 `w24-s5-descriptor-structure-replay-scaffold/2`
已删除 Phase-B 的重复 legacy raw validator，并调用 Writer-owned
`w24-s5-shared-legacy-raw-replay/1`。`W24S5LegacyRawReplayPins` 只是 caller-supplied 结构 pins，
不是 trust/verdict/terminal/transition/advance authority；replay 仍要求 reader 私有 issuer 签发的
`CandidateReplayAuthority`，输出也仅为 Writer 私有 issuer 的 scalar/hash opaque projection，不暴露
`JObject`、`JToken` 或 raw bytes。当前绑定为 Writer source
`sha256:1550c64bc744562d69c0ce0953d9514869627e65a2a97e5886e09b84ac2df06d`、Machine source
`sha256:aeb79cf54307047a6d5b42bee69a98a177530098484047d56e6914c21eb0f9cb`、Machine tests
`sha256:2fe05bc7d2f56d8991b62202976f48e1aa9f0ec5ad0741ab0147554e5b136129`；combined current source、
current Writer+Phase-B focused test sources 及 no-`UNITY_INCLUDE_TESTS` production source 均静态编译 0 error。r28
随后在 `.codex_tmp/w24-fresh-20260825-0628/project` 对这些 canonical/shadow 一致的当前 source/test hash
执行两条 `EditMode -nographics` focused runs：

- Writer `W24S5EvidenceRevisionWriterTests`：PID `35496`、外层 exit `0`、`42/42 passed`，无失败/跳过/未决；
  XML `2026-08-26 02:01:42Z → 02:01:47Z`、`duration=4.7854052s`、
  `sha256:1fb61e96c8c3dcc6d6dc9d34a68e67f8da5034e74dd87cf5c16bc6de9eee516a`、
  `LastWriteTimeUtc=2026-08-26T02:01:47.5230791Z`；log
  `sha256:7427849156576a9cacfadc07a11fc1e322a12c541a362e978afc229206e28eec`、
  `LastWriteTimeUtc=2026-08-26T02:01:48.0276695Z`。
- Phase-B `W24S5MachineFailureProducerTests`：PID `9228`、外层 exit `0`、`13/13 passed`，无失败/跳过/未决；
  XML `2026-08-26 02:02:04Z → 02:02:13Z`、`duration=8.6847156s`、
  `sha256:ea4763a6fef47689e616bfb16452848327dc921b3b13ae204c46d3f80f6c6e19`、
  `LastWriteTimeUtc=2026-08-26T02:02:13.7099747Z`；log
  `sha256:62a433dc39e57aace61862074bbc4e1159b91028d02d02958bfd55d6d20a315f`、
  `LastWriteTimeUtc=2026-08-26T02:02:14.2172111Z`。

两份 log 均记录结果落盘、Input System shutdown、licensing channels disconnect、`Cleanup mono` 与自然完成。
CandidateReader focused suite 本次未重跑，r25 `4/4` 仍是其最新直接证据；r26 Writer 与 r27 Phase-B
保留为历史源码绑定。r28 只验证当前 shared raw Writer/consumer focused 行为，不关闭 persisted
descriptor/schema/snapshot/evaluation validator、evaluator runtime/provenance、terminal 或 route authority。

真实 gate-owned evaluator 仍须共享 persisted descriptor/schema/snapshot/evaluation validator、取得 repository-trusted hermetic runtime lease、从 sealed typed raw 独立重跑冻结工具并比较 native
rerun 结果，之后才可定义 terminal/report/route receipt 的 typed self-hash DAG、原子发布和私有 authority。
S0b 目前也缺可独立重算 receiver ROI 的 typed raw，因此继续 fail-closed。完整边界见
`docs/vfx-reviews/W24_S5_MACHINE_FAILURE_PRODUCER_STAGE_REPORT.md`。

### 4.5 Candidate / evidence read-only replay（legacy C0 focused verified；descriptor test-only / evaluator pending）

新增 `W24S5CandidateEvidenceReader` 将候选身份重放与机器判定彻底分离。当前源码
`Editor/W24/S5/W24S5CandidateEvidenceReader.cs` SHA-256 为
`ed3908778d38778bf2009db63d6a50b602e11af071fdcb374d8283a4f48408a5`，测试
`Tests/EditMode/W24S5CandidateEvidenceReaderTests.cs` SHA-256 为
`3dd27aa4a0f9badb7ce1df4a3cf2980e96ca308508a2a4f4f159c0adfe14eead`；canonical 与 shadow
字节相同，Bee/Roslyn Editor 与 EditMode Tests 静态编译均 exit `0`。

legacy `w24-candidate/1.0` 路由按真实 C0 schema 分别重放 pinned bootstrap Contract/Trace、
candidate-local Contract/pending Trace、19-field Manifest snapshot、evidence-free formal binding、
owned bytes/meta/exact root、Preview/camera 与 candidate static tree。它不会把 bootstrap Contract hash
与冻结后重新计算的 candidate Contract hash混为一谈，也不会把真实 production Manifest 当成五字段
合成夹具。资产哈希是 byte-oriented；只有 JSON/text 输入要求 strict UTF-8。`ReplayCandidateOnly`
现只在相同候选重放完成后签发私有 opaque authority，Phase-A/B 不从可变 Snapshot 或错误文本推断有效性。
四份真实 legacy C0 仍没有 production-admitted E1 descriptor，且没有 E2 namespace，因此普通 E1/E2
都在候选 Snapshot 建立后明确返回 `INVALID`，不产生 machine verdict；Phase-A writer 只在 test registry
下可写 scratch descriptor，production 为 `REGISTRY_PENDING`。

r25 当前源码隔离 focused run 使用 PID `42992`，外层 exit `0`，NUnit `4/4 passed`、无失败/跳过/未决，
XML 时间 `2026-08-25 23:19:25Z → 23:19:25Z`、`duration=0.4208484`；XML SHA-256
`df14dc3e0d07cf0de2556ba4153d2ce9766d30d2afac116cd958659ee9d20f6f`，log SHA-256
`d6a3900aaf74c7495f039676ff2a9877627e5e6f553758db30d1625c830f82ff`。日志自然完成两路 licensing
disconnect 与 `Cleanup mono`。r24 是被当前源码结果取代的较早 accepted run；r23 的 `2/4` 与
timeout/forced-stop 仅保留为已替换的 schema 诊断，均不计当前最终证据。

这不是 production evaluator 完成：C1/C2 当前仍是 test-only transaction，缺 predecessor/semantic/
Manifest-input/capture-tool-input 的正式 evidence replay；legacy C0 也缺候选本地 bootstrap source snapshot，
legacy raw replay 已抽成一个 Writer-owned opaque shared core；但 persisted descriptor/schema/snapshot/evaluation
validator 仍在 Writer 与 Phase B 各自实现，且 production registry/runtime 均 pending。
完整边界与后续 schema/writer/evaluator 顺序见
`docs/stage-notes/W24_CANDIDATE_EVIDENCE_READER_REPORT.md`。任何机器失败签发、Visual QA、L3/L4、
Publication 或用户裁决仍为 pending。

## 5. 明确延后项

- W-C1/W-C2 的旧候选拒绝记录保持不可改写；在用户后续“全项目开发推进、视觉最后验收”
  授权下，新执行载体可以作为全新 `VISUAL_PENDING` 候选开发，但不得改写旧拒绝或冒充通过；
- 不对 W-C3、W1–W18 或 W0 历史视觉条目虚构 L3/L4；
- S0a 已完成 reduced66 捕获、盲投影和三次 advisory Visual QA，但尚无人工标签/校准终态；
  S0b/S3 机器证据已封存，S4 裁决和所有用户 L4 仍按 W24 既定权限执行；
- 既有资产的批量合同补录、迁移、保留/重做/废弃裁决仍需用户清单与视觉/所有权验收；
- 不安装包、不调用第三方模块、不改变用户正式视觉资产。
