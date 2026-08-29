# ADR-004：Windows Broker 安装、生产只读与 Unity Worker 进程边界

> **SUPERSEDED FOR PRODUCT DELIVERY (2026-08-28).** `ADR-005_USER_MODE_BROKER_WORKER_ARCHITECTURE.md` is the sole current Phase-2 architecture. Everything below is retained byte-for-byte as historical dormant-route provenance: it is not a current implementation or audit queue, dependency, blocker, or activation prerequisite. In particular D1/D1R, ServiceHost/install, I1, R1, A1, B1 and the privileged route must not continue. Only C1/C2 and separately reviewed ordinary-user-compatible P1/S1 fragments may enter U0-U6 under ADR-005.
>
> Current replacement architecture token: `USER_MODE_LOCAL_CREATIVE_TOOL_V1`.
>
> Post-closeout update (2026-08-29): U0–U6 and A0–A6 are closed and no work package from this ADR's route is active. The in-body status lines below (`Accepted — architecture freeze only`, `fresh independent audit pending`) and every numbered section heading are part of the frozen historical body; no audit is pending and no privileged node may resume. Sequencing for all new work is owned solely by `docs/plans/OPTIMIZATION_MASTER_PLAN.md` (the P0/R/O/F milestone series), with day-to-day task acceptance per `docs/plans/CODING_STANDARDS.md` and no per-task receipt requirement.

状态：`Accepted — architecture freeze only; implementation and production activation remain NO-GO`  
Package remediation state: `WP-P2-PRODUCTION-READ-DAG-REBASE-1` documentation remediation complete; fresh independent audit pending; neither implementation nor package GO  
日期：2026-08-28  
决策人：用户（Phase 2 production-read architecture freeze）  
关联：ADR-002、ADR-003、`WP-P2-PRODUCTION-READ-GATE-FREEZE`

## 1. 决策与不授予事项

Windows production-connected profile 只能采用下列受控拓扑：

```text
Desktop (untrusted for project paths and project I/O)
  -> authenticated local named pipe -> Broker ServiceHost
  -> separately authenticated local named pipe -> dedicated Broker-created Unity Worker
  -> Broker-issued handle capability -> Unity project-content/Unity API work
```

这是一份架构冻结，不是 production activation。它不改变当前 `Program` 和
`BrokerPolicy` 的 fail-closed 行为：在所有下述前置节点冻结、实现、安装并独立
审计前，Broker 必须在 listener、caller-path、项目或环境 I/O 之前输出
`W24FS001` 并以 `23` 退出。

本 ADR 不授权：调用 SCM、安装/启动服务、创建 production listener、注册项目、
读写项目内容、启动 interactive Unity Editor、Desktop 直接 I/O、Worker command、
视觉/用户/L3/L4 authority，或把既有 test/legacy 证据升级为 production 证据。

## 2. 术语和不可降级原则

- **Broker**：Windows production profile 的唯一 registration issuer、会话与
  capability 编排者；它不是 Unity 项目内容读取者。
- **ServiceHost**：Broker 的固定 Windows 服务宿主。`Running` 是可审计的运行
  状态，不是“进程存在”或“SCM 已启动”的同义词。
- **protected install object**：由 installer/host 从 canonical Volume-GUID/NTFS
  命名空间、逐段 no-follow 打开的安装文件对象及其祖先对象；它带有实际 ACL
  readback、文件 ID、内容哈希、长度和保持中的对象 pin。
- **launch receipt**：单次启动的、host-owned、one-use 关联记录；它同时绑定
  durable profile、SCM 配置、protected install object、PID/epoch/token/service
  SID/session、Broker generation 和 nonce。它不是 wire DTO，也不是 authority。
- **project locator**：host-owned 的 Volume-GUID/repository/project identity
  capability；不是 Desktop、Client 或 Unity wire 中的可选绝对路径。
- **ordinary product profile**：本 ADR 采用的普通 .NET/Windows product profile。
  其明确字段为 `LoadedImageVerified=false`。
- **higher-assurance profile**：另行授权的 signed driver/WDAC/CI 设计。它不能
  被普通 profile 隐式要求、模拟或声称已经满足。

任何较弱事实均不得推断为较强事实。特别是 PID、路径、同一哈希、SCM 启动成功、
pipe 连接成功、测试的句柄或 UI 状态都不构成 loaded-image、项目 admission 或
authority 证明。

## 3. 可执行映像 claim taxonomy

四种 claim 是不同对象、不同证据源和不同保证强度。生产 receipt 必须携带其
claim kind；consumer 必须拒绝未知、缺失或试图提升 claim kind 的输入。

| Claim | 事实来源与可陈述内容 | 绝对不能推断 | 当前/普通 profile 位置 |
|---|---|---|---|
| `NativeProcessPathObserved` | `QueryFullProcessImageNameW`、PSAPI 或同等 OS API 从已钉住 process object 返回的 native path 文本。 | backing `FILE_OBJECT`、启动时文件对象、字节一致性、签名、当前内存页。 | 仅 supplementary peer/process fact。 |
| `ProtectedLaunchFileObserved` | installer/host 对受保护安装 leaf 和 ancestor 的 no-follow object pin、实际 ACL readback、file ID、length、content typed hash 与启动窗口保持。 | process 实际映射此文件、process backing object、内存页完整性。 | 普通 profile 的上限；此时 `LoadedImageVerified=false`。 |
| `ProcessBackingFileObjectVerified` | 由获授权的 kernel/CI 路径把 exact process executable backing file object 与 protected launch file object 关联。 | 当前内存页未被修改。 | 普通 profile 不实现、不声称。需要单独 ADR、签名 driver 或 WDAC/CI 设计及独立 gate。 |
| `CurrentExecutableMemoryVerified` | 获授权的内核/Code Integrity 设计对当前 executable image page/section 完整性做出的明确结论。 | 任何未来配置、其他 process 或 authority 结论。 | 当前未设计、未实现、未宣称。 |

普通 user-mode post-start API 无法证明 strict `loaded image bytes == file bytes`：
`QueryFullProcessImageNameW`、PSAPI 和 `NtQueryInformationProcess` 给出路径或
metadata；重新按路径打开并哈希得到的是随后选择的 file object；
`ReadProcessMemory` 也不是 raw PE/backing-object integrity proof。因此 strict
backing-file identity 只能在独立授权的 `PsReferenceProcessFilePointer` /
`PsSetCreateProcessNotifyRoutineEx` signed-driver，或经过明确设计和审计的
WDAC/CI 路径中讨论，不能以普通 .NET implementation 替代。

### 3.1 既有 `process-image/1` 兼容事实

`WindowsNamedPipePeerFactsSource` 当前从 process native path 重新打开文件，
并计算 `process-image/1`。它可保留为兼容 peer-facts/correlation fact，且仍可
用于既有 test-scaffold 的严格负例；它**不是**已加载映像 proof，不能单独作为
production launch-file admission、`ProcessBackingFileObjectVerified` 或
`LoadedImageVerified=true` 的输入。production path 必须消费下文定义的独立
launch receipt；路径 reopen hash 只能是附加、可失效的兼容事实。

## 4. 普通 production profile：launch-correlated protected-file evidence

普通 profile 的成功状态固定为：

```text
LaunchFileProtectedAndCorrelated = true
LoadedImageVerified = false
```

这表示服务启动窗口内一个 installer-owned install file object 被连续保护并与
实际 service process 的 OS facts 关联；它不表示该 process 的 image section
backing object 或内存页已经验证。

在 ServiceHost 可以报告 `Running` 或接受任何生产请求前，host/installer 必须
完成并记录以下不可替代的步骤：

1. 只从 host-owned canonical Volume-GUID local-NTFS namespace 取得 install
   root；逐段 relative/no-follow 打开 fixed executable leaf 与每个祖先。UNC、
   DOS device remap、caller path、reparse/junction/symlink、ADS、remote volume 和
   不可验证文件系统均失败关闭。
2. 对 leaf 和祖先执行 actual security-descriptor/ACL readback。普通 Desktop
   user 与 service identity 都不得拥有 write、delete、rename、`WRITE_DAC` 或
   `WRITE_OWNER` 能力；任何 broad/inherited/unknown ACE、owner/group drift 或
   readback 不等于 frozen policy 都失败关闭。
3. 取得并固定 executable file ID、volume identity、exact byte length 和
   `vfxcomposer.executable-content/1` content typed hash。对未预期 hardlink、
   reparse、non-regular file、size/identity/hash drift 或双读不稳定均失败关闭。
4. 在计算哈希之前取得 leaf/ancestor pins，并从哈希开始跨越 SCM start、launch
   receipt 生成和 session admission 保持 no-write/no-delete protection。pin
   丢失、权限变化、对象替换或意外 handle ownership 均撤销该次启动；不能以路径
   reopen、PID 文本或重算的相同摘要补救。
5. 将 durable authenticated profile digest、已 readback 的 SCM configuration
   digest、protected file identity、Broker generation 和 cryptographically random
   one-use nonce 绑定为启动 intent。nonce 在同一单一 issuer/持久 replay store 中
   只能消费一次；重启、generation 变化或未完成启动均使旧 receipt 无效。
6. 仅在上述 intent 仍 live 时请求 SCM start。SCM 返回的 PID 只是 locator；host
   必须 pin exact process object，重放 PID、creation epoch、token user、enabled
   service SID、session、native path observation，并把这些 facts 连同 nonce 和
   profile digest 写入 launch receipt。PID reuse、wrong SID/session/epoch/generation
   或 token group 语义不符均失败关闭。
7. ServiceHost 只在 launch receipt、protected pins、Worker supervision、project
   enrollment，以及**第一个实际 serving named-pipe instance** 的 ACL
   application/readback receipt 都 live 时才可转入 `Running`。该 serving instance
   的 receipt 必须在 `ConnectNamedPipe`/accept 前，从同一 pipe object 读回 exact
   owner、group、DACL、SACL、ACE type/order/SID、access mask 与 protection flags；
   non-serving bootstrap receipt 不能替代它。任何以后 drift 先撤销
   listener/session/lease，再终止或停止关联 Worker，最后把服务置为 bounded
   stopped/pending；不得保留“看似成功”的状态。

SCM 设置、installer policy candidate、in-memory executable identity policy 和
caller-supplied pinned-handle observation都不是上述 receipt 的替代品。它们只有在
各自已审计的 dormant 范围内有效。

## 5. Pipe、ServiceHost 与失败关闭顺序

production transport 仍仅限本机 authenticated named pipe。不存在 HTTP、任意
TCP、production stdio、Desktop→Worker 直连、interactive-Editor fallback 或
caller-path fallback。

actual pipe ACL 是**每一个 serving named-pipe instance** 的前置条件，不是
bootstrap 的一次性替代物。host 可以创建 non-serving bootstrap object 来保留候选
namespace、验证 profile/template 或探测 collision；它不得 `ConnectNamedPipe`、
不得 accept client，且它的 ACL receipt 永远不能作为任一 serving instance 的
receipt。

对每一个准备服务 peer 的 named-pipe instance，host 必须按以下原子顺序完成，且
在 `ConnectNamedPipe`/任何 accept 之前停止：

1. 新建该 exact serving instance，并以 namespace collision/既有对象为失败；
2. 对**该 instance** 应用 frozen durable-profile security descriptor；
3. 从同一 serving pipe object 立即 read back owner、group、DACL、SACL、每个
   ACE 的 type/order/SID/allow-or-deny/inheritance flags/opaque data、access mask
   和 security-descriptor protection/control flags；将它们与 frozen profile 的
   exact expected presence/absence、顺序和值比较；
4. 将 instance identity、profile/generation、readback 的完整结果和 comparison
   verdict 写入该 instance 的 receipt，并保持对象/receipt 仍 live。

第一个将承担 production request 的 serving instance 必须已有上述成功 receipt，
ServiceHost 才能报告 `Running`；它在此之前仍不得 accept。每个随后创建的 serving
instance 都重复同一 apply-and-readback sequence，单独失败关闭；先前 instance、
bootstrap receipt 或 template comparison 均不能为它背书。pipe squatting、既有
对象、错误 owner/group、broad/deny/inherited/unknown ACE、unexpected/missing SACL、
mask/protection drift、readback failure 或 namespace collision 都使该 instance
在 accept 前关闭，并撤销未发布 capability；不得先监听再“稍后修正 ACL”。

每个 serving session 还必须绑定 actual peer SID、pinned PID、process epoch、
approved profile image fact、session ID、Broker generation、nonce/receipt
generation 和 negotiated protocol/capability version。连接建立不等于项目 lease，
更不等于 authority。

生产失败关闭顺序冻结如下：

1. 无 durable profile、可信 install object、live launch receipt、**第一个 serving
   instance** 的 actual ACL readback、Worker job 或 project enrollment：维持
   `W24FS001`/`23`；零 listener、零 serving accept 和零项目 I/O。
2. 任一 serving pipe 在 apply/readback/namespace/peer 基础事实不符，或其 receipt
   不再 live：该 instance 不得 `ConnectNamedPipe`/accept，关闭该 pipe 并撤销未发布
   capability；bootstrap object/receipt 不能补救。
3. process/worker/project identity、nonce、epoch、SID、session 或 generation
   不符：撤销 receipt、lease 和 in-flight request；不按 PID、路径或超时复活。
4. strict wire parsing、schema、version、capability、request correlation 或 typed
   hash 不符：关闭相关 session；零 project-content I/O。
5. 仅在已认证 Worker 的 exact handle capability 和 host-owned locator 都 live
   时，Unity Worker 才能执行 allow-listed read。Broker/Client/Desktop 永不退回
   filesystem 读取。
6. Worker crash、Job loss、ACK 超时、unresolved native handle 或 enrollment drift：
   close the Job to end the child lifecycle、revoke handles/leases、失败请求；不
   partial promote，也不将 stale receipt 解释为成功。Job 是 lifecycle containment，
   不是 filesystem/capability sandbox。

`Program` 和 `BrokerPolicy` 在 B1 之前保持 `W24FS001`/`23`，且该 gate 位于
listener/path/project I/O 前。B1 是唯一可提议改变这个 production entry ordering
的包；其余节点一律不得绕过该顺序。

## 6. 专用 Unity Worker 拓扑

production Unity Worker 是 Broker 创建、Broker 监督的专用进程，绝不是用户当前
interactive Editor：

1. Broker 创建 child process 时保持 suspended；立即把 exact process handle
   放入 kill-on-close Windows Job，确认 Job assignment 成功后才 resume。该 Job
   只负责 child lifecycle containment（父/host 关闭时终止 child）；它不是
   filesystem、token、code-integrity 或 capability sandbox。
2. Job assignment、global ownership record、Worker process pin、pipe handles、
   project root capabilities、launch nonce 和 ACK state 的 owner 必须是明确的单一
   host/Broker component。裸 PID、裸 numeric handle 或 process-name 不能作为
   ownership/recovery key。
3. Worker 只接受 host-owned Volume-GUID project locator 和 Broker-duplicated
   least-privilege handles；它不接收 Desktop path、environment/EditorPrefs、
   active interactive project 或普通 JSON registration。
4. Worker 在接收 locator 后必须 exact ACK，并以该 capability 相对/no-follow
   打开内容。无法 join Job、无法接受 locator、ACK identity drift、Worker restart、
   unresolved handles 或 supervisory ownership 丢失都是 STOP；先终止 child/Job，
   再撤销 session。不存在 interactive Editor、same-process test peer 或 path-based
   fallback。

Worker 只拥有 Unity/AssetDatabase/project-content 访问；Broker 只路由、验证、
复制和撤销 capability；Desktop 只显示 broker-issued project identities/status。

### 6.1 Ordinary-profile containment boundary

ordinary product profile 以**进程生命周期和 authenticated capability routing**为
边界，而不是以 OS sandbox 为边界。S1 的 Job 防止受监督 child 在 host 生命周期
结束后继续存活；它不限制 Worker 已获进程中的文件系统访问、token、loaded code、
Win32 API 或已获 handle 的滥用。Broker-issued handles 也不是 capability-only
OS containment claim。

本 ordinary-profile threat model 仍要求防御未获认证同一用户进程对 pipe
impersonation/squatting、namespace/path replacement、junction/reparse/DOS remap、
receipt replay、PID reuse、epoch/session/generation substitution 及跨 session
混淆的攻击。这些攻击在 peer/namespace/object admission 边界发生，必须在
authentication、serving-instance ACL readback、pinned-object replay 或 lease
revocation 中 fail closed。

下列情况明确**排除**在 ordinary profile 的防御承诺之外：已经把恶意代码注入
已认证 Desktop 或 dedicated Worker 的攻击者，以及被 host 明确 enrolled 的 Unity
project 中恶意 Editor assemblies。显式 enrolled project code 在本 profile 中被
视为被信任可执行代码；Job、pipe ACL、locator 或 duplicated handle 均不把它变成
不可信 sandbox input。这个排除不允许把 caller path、interactive Editor、
un-enrolled project 或未认证 peer 带回 production route。

若产品需要抵御已认证进程 code injection 或 malicious enrolled project code，
必须新建独立 high-assurance profile/package，并单独设计、部署和审计 restricted
token and/or AppContainer、WDAC/HVCI/code-integrity policy、sandbox/virtualization
及其 recovery/evidence。它们不是 S1/B1 的隐含交付物，当前 ADR 不声称已实现任何
此类 OS containment。

## 7. Host-owned project enrollment

项目 enrollment 只能由 installer/host 在已验证 local NTFS volume 上完成。它提交
Volume-GUID、volume/file identities、repository/project root identities、profile
digest 和 enrollment generation；Desktop 只能从 Broker 枚举 opaque registered
project selection，不能提交绝对路径、UNC、drive letter、ADS、DOS device 或
repository JSON。

enrollment 时与每次 lease/Worker grant 时都必须检测：volume/file identity、
ancestor no-follow identity、reparse/hardlink policy、profile/generation 和 locator
capability 是否漂移。任何项目 enrollment drift 会撤销整个 project session，而
不是挑选单个查询“继续运行”。Unity Worker 是唯一最终读取项目内容的组件。

## 8. 生产对手与必须失败的情况

下表是最低 adversarial gate；“通过”只表示该攻击在对应 node 的冻结测试/receipt
中失败关闭，不表示更强 claim 已获得。

| 攻击或错误 | 必须的防御/receipt | 未满足时 |
|---|---|---|
| 安装路径替换、hash-start 后 swap | pre-hash leaf/ancestor pins、hash/file-ID/length replay、no-write/no-delete lifetime、launch receipt | 不启动/不 Running；不以路径 reopen 补救。 |
| hardlink、junction/symlink/reparse、DOS-device remap、UNC/ADS | Volume-GUID local NTFS admission、relative no-follow、exact reparse/link policy、native identity replay | 拒绝 enrollment/install/Worker locator。 |
| 相同摘要但不同 object，或 file identity/length drift | exact volume + file ID + length + typed hash；claim kind 不可提升 | receipt/lease 无效。 |
| PID reuse、错误 token/service SID/session/epoch/generation | pinned process object、token group semantics、session/epoch/generation replay | 关闭 session，不按 PID 文本复活。 |
| pipe squatting、错误 ACL 或 ACL 竞态 | 每个 serving instance 在 `ConnectNamedPipe`/accept 前 apply+readback exact owner/group/DACL/SACL/ACE/mask/protection；首个 serving receipt 是 Running 前置；bootstrap 仅辅助、不能替代。 | 该 instance 不 accept；首个失败则不 Running。 |
| 已认证 Desktop/Worker code injection，或显式 enrolled project 的恶意 Editor assembly | ordinary profile 明确不把 Job/handle 当 sandbox；显式 enrolled project code 为 trusted executable code。 | 不作 ordinary-profile containment claim；需要独立 restricted-token/AppContainer/WDAC/HVCI/sandbox profile。 |
| nonce/launch receipt replay | durable single-owner one-use nonce store、profile/generation binding、reboot/restart invalidation | 拒绝 replay，撤销旧 lease。 |
| Worker crash、Job assignment/ACK 失败、restart、unresolved handles | suspended→Job→resume、kill-on-close lifecycle containment、global ownership record、exact close/ACK/revoke protocol | 终止 child/Job；零 stale-handle reuse；不声称 sandbox。 |
| project enrollment drift 或 cross-project substitution | host-owned locator、volume/repository/project identity binding、generation revocation | 整个 project session fail closed。 |
| Desktop/Client 直接读写 Assets/Packages/ProjectSettings | static dependency/API gate、runtime filesystem sentinels、Broker-only selection | package失败；不建立 Desktop fallback。 |
| test issuer/HandleProbe/test pipe/scratch/legacy receipt leakage | production package scan、separate build configurations/identities、receipt provenance gate | production B1/E1/A2 失败。 |

普通 profile 不防御 administrator、kernel 或 raw-volume compromise。这些属于
higher-assurance profile：需要明确的 platform Code Integrity、WDAC/CI 和/或
signed-driver threat model、部署及恢复证据；当前产品不得暗示已对它们防御。

## 9. Rebased 13-node production-read DAG, unique leaves, governed sequential overlays and the narrow W1 composition shim

G0 = WP-P2-PRODUCTION-READ-GATE-FREEZE 是 docs-only 历史前置冻结，不计入本节
13 个未来 implementation nodes。G0 的历史快照
`DAG_NODES=12 / DAG_EDGE_GROUPS=6 / DIRECTED_EDGES=15 /
EXACT_OWNED_FILES=68 / RECEIPT_ROOT_EXCEPTIONS=1` 仅是 provenance，不能被
重述为当前 DAG 或全局零重叠结论。

本节是唯一的 likely ownership envelope，不是目录授权。每个 writer/auditor 都
必须是 fresh、分离的任务，并在其有界 package 结束时 STOPPED。当前计数为
`DAG_NODES=13`、`DAG_EDGE_GROUPS=8`、`DIRECTED_EDGES=17`、
`UNIQUE_LEAVES=75`、`SEQUENTIAL_OVERLAYS=12`、
`RECEIPT_ROOT_EXCEPTIONS=1`。75 个 unique leaves 是 repository-relative
exact file path；唯一的 receipt-root exception 仍仅为 A2 的
`.codex_tmp/WP-P2-PRODUCTION-READ-FINAL-AUDIT/**`，它不是 author-controlled
source ownership、目录授权或对未知 future receipt filename 的授权。

没有未批准的 collision：`UNAPPROVED_COLLISIONS=0` 是每个 package 开始前的
可复现检查，而不是“全局 zero overlap”声明。下文精确列出的 12 条 sequential
overlays 是唯一允许重复使用的 paths：每条都要求一个 active writer、先验证
exact prior SHA-256、该 overlay group 内无并发写入、完成后 fresh independent
audit；任何 drift、并发、缺失 pre-hash 或额外 path 都 STOP。所有未列为 overlay
的 new leaves 必须 pre-absent 且 case-insensitive collision count 为零。

| Node / package | 依赖 | 唯一目标与 likely file ownership | 最小 gate、receipt 与 STOP |
|---|---|---|---|
| C1 `WP-P2-PROTOCOL-PROJECT-SELECTION` | G0 | 无路径 registered-project selection contract：DTO `src/VFXComposer.Protocol/Projects/RegisteredProjectSelection.cs`；唯一 wire codec `src/VFXComposer.Protocol/Json/StrictWireCodec.cs`；registry `src/VFXComposer.Protocol/MessageKinds.cs`、`src/VFXComposer.Protocol/WireSchemaRegistry.cs`、`src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs`；tests `src/VFXComposer.Protocol.Tests/RegisteredProjectSelectionTests.cs`、`src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs`、`src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs`、`src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs`、`src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs`；schema verifier `eng/verify-phase2-schemas.py`；exact schema `docs/schemas/desktop/vfxcomposer-registered-project-selection-v1.schema.json`。 | schema/strict-codec/golden/negative path tests；无 Broker/Client/Unity runtime。任何 path/authority field STOP。 |
| C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` | G0; C1 must already be closed before any C1→C2 overlay | Pure C# locator/locator-ACK contract, exactly 16 paths: new `src/VFXComposer.Protocol/Registration/WorkerProjectLocator.cs`, `src/VFXComposer.Protocol/Registration/WorkerProjectLocatorAcknowledgement.cs`, `src/VFXComposer.Protocol.Tests/WorkerProjectLocatorTests.cs`, `src/VFXComposer.Protocol.Tests/WorkerProjectLocatorGoldenVectorTests.cs`, `src/VFXComposer.Protocol.Tests/GoldenVectors/desktop-phase2-worker-project-locator-v1.json`, `docs/schemas/desktop/vfxcomposer-worker-project-locator-v1.schema.json`, and `docs/schemas/desktop/vfxcomposer-worker-project-locator-ack-v1.schema.json`; sequential overlays `src/VFXComposer.Protocol/Json/StrictWireCodec.cs`, `src/VFXComposer.Protocol/MessageKinds.cs`, `src/VFXComposer.Protocol/WireSchemaRegistry.cs`, `src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs`, `src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs`, `src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs`, `src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs`, `src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs`, and `eng/verify-phase2-schemas.py`. Existing `GoldenVectors/**` inclusion makes this sufficient without a csproj or lock edit. | Strict DTO/schema/codec/registry and exact .NET golden bytes only; the later W1 test must replay those same immutable bytes with Unity. No path, URI, root text, raw handle, runtime, transport, I/O, lease, grant, session issuer, command or authority. Any csproj/lock need, hidden protocol fork, or absent/colliding new leaf STOP. |
| D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION` | G0; D1_0 is retained STOPPED/NO-GO provenance, not a GO dependency | A fresh, sequential overlay of exactly the same three D1_0 files: `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs`, `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs`, and `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs`. It repairs every internal strict store-file open to request `ACCESS_SYSTEM_SECURITY`, pending publish rename to hold required `DELETE`/target-directory semantics, native secret zeroing before free, distinct least-privilege root/file ACL predicates, exact suffix rollback and ordinal manifest ordering. | Core acceptance is dormant/static/fail-closed remediation only: strict open must reject without the I1-owned root/token; no successful privileged store open, commit, reopen or readback may be claimed. D1R→I1 is code dependency only. Caller/project JSON, environment trust root, in-memory substitute, live-success overclaim or a fourth source/test file STOP. |
| W1 `WP-P2-PRODUCTION-UNITY-WORKER-CONNECTOR` | C2、ADR-003；only the narrow §9.2.1 composition shim | dedicated Worker connector and **locator ACK**, not handle-grant ACK, and never a second normative protocol implementation. **全部** W1 new Unity C# files remain exactly `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6DedicatedWorkerConnector.cs`, `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6HostOwnedProjectLocator.cs`, `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6ProductionWorkerWireCodec.cs`, and `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6ProductionWorkerConnectorTests.cs`; their four exact metas are `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6DedicatedWorkerConnector.cs.meta`, `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6HostOwnedProjectLocator.cs.meta`, `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6ProductionWorkerWireCodec.cs.meta`, and `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6ProductionWorkerConnectorTests.cs.meta`; new `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production.meta` belongs only to W1. The historically named `W24S6ProductionWorkerWireCodec.cs` is a zero-semantics composition facade only: it may call the frozen ADR-003 canonical `Worker/Protocol/W24S6WorkerProtocolCodec` primitives and consume C2 schemas/canonical golden bytes, but may not declare an independent DTO, hash/type, canonicalization, registry/token entry, message kind, schema or codec. Its one new test path may only replay the frozen ADR-003 adapter together with C2 vectors. | Dormant strict C2-wire/vector parity and no-interactive fallback only; no pipe, project read, session issuer, handle grant/ACK substitution or authority. §9.2.1 preserves ADR-003's single normative adapter semantics and forbids a protocol fork. Existing ADR-003 `Worker/Protocol/**` and `Tests/EditMode/W24S6WorkerProtocolTests.cs` stay frozen/read-only dependencies; existing `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker.meta` and `project/Packages/com.vfxcomposer.unity/Tests/EditMode.meta` stay frozen/non-owned. Any Unity main-UI edit, net8 copy, caller path, independent wire surface or tenth W1 file STOP. |
| I1 `WP-P2-SCM-INSTALLER` | D1R, for code only | controlled install/uninstall/rollback and actual SCM/install-root configuration: `services/VFXComposer.Broker/Installation/WindowsScmInstaller.cs`, `services/VFXComposer.Broker/Installation/ProtectedInstallRoot.cs`, and `services/VFXComposer.Broker.Tests/WindowsScmInstallerTests.cs`. I1 exclusively provisions and pins the local-NTFS root with enabled `SeSecurityPrivilege`, exact root/store `ACCESS_SYSTEM_SECURITY`, and target-directory rename rights; its integration test performs the **first** real strict D1R store commit/reopen/readback. | Clean-machine SCM/config/ACL/readback plus an I1-owned privileged live-store receipt binding root identity, enabled privilege, access rights, D1R source identity and profile/generation. No listener/project read. Package/config broadening, unverified account/payload semantics, or treating D1R core evidence as live-store success STOP. |
| A1 `WP-P2-LAUNCH-CORRELATED-SERVICE-ATTESTATION` | I1 | **只**实现 ordinary-profile launch-correlation primitives：`services/VFXComposer.Broker/Configuration/LaunchCorrelationReceipt.cs`、`services/VFXComposer.Broker/Security/WindowsProtectedLaunchPin.cs`、`services/VFXComposer.Broker/Security/WindowsServiceLaunchCorrelation.cs`、`services/VFXComposer.Broker.Tests/WindowsServiceLaunchCorrelationTests.cs`。A1 不拥有 ServiceHost entrypoint、project、SCM callback 或 `Running` guard。 | pins/ACL/file-ID/hash/length/process token/SID/session/epoch/nonce/generation replay across controlled SCM start；receipt says `LoadedImageVerified=false`。path-reopen as loaded proof or unpinned launch STOP。 |
| P1 `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION` | D1R | per-serving-instance production pipe ACL application/readback and peer-session base：`services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs`、`services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs`、`services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs`、`services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs`。 | every serving instance applies and reads back exact owner/group/DACL/SACL/ACE/mask/protection before `ConnectNamedPipe`/accept；first serving receipt precedes `Running`；later instances independently fail closed。`CurrentUserOnly`/test issuer、bootstrap substitution 或 listen-before-readback STOP。 |
| S1 `WP-P2-WORKER-SUPERVISION-GLOBAL-OWNERSHIP` | D1R | suspended→Job→resume, kill-on-close **lifecycle containment** and exact global ownership：`services/VFXComposer.Broker/Ipc/WorkerProcessSupervisor.cs`、`services/VFXComposer.Broker/Native/WindowsJobObject.cs`、`services/VFXComposer.Broker/Ipc/WorkerGlobalOwnershipRegistry.cs`、`services/VFXComposer.Broker.Tests/WorkerProcessSupervisorTests.cs`、`services/VFXComposer.Broker.Tests/WindowsJobObjectTests.cs`。 | Job-before-resume, crash/restart/unresolved-handle cleanup and no-PID-reuse proof；no interactive fallback。Job/capability sandbox claim or failure to contain lifecycle STOP。 |
| R1 `WP-P2-HOST-OWNED-PROJECT-ENROLLMENT` | I1 | installer/host-only local-NTFS enrollment and Volume-GUID locator：`services/VFXComposer.Broker/Registration/HostOwnedProjectEnrollment.cs`、`services/VFXComposer.Broker/Native/WindowsProjectLocator.cs`、`services/VFXComposer.Broker.Tests/HostOwnedProjectEnrollmentTests.cs`、`services/VFXComposer.Broker.Tests/WindowsProjectLocatorTests.cs`。 | volume/repository/project pin/readback and junction/UNC/ADS/remap/drift negatives；no Desktop submission/content read。any caller path STOP。 |
| B1 `WP-P2-PRODUCTION-BROKER-READ-CONVERGENCE` | C1 + C2 + W1 + A1 + P1 + S1 + R1 | the only convergence node and **sole owner of final ServiceHost Running admission**：`services/VFXComposer.Broker/Program.cs`、`services/VFXComposer.Broker/Configuration/BrokerPolicy.cs`、`services/VFXComposer.Broker/Ipc/NamedPipeBrokerHost.cs`、`services/VFXComposer.Broker/Ipc/WindowsNamedPipePeerFactsSource.cs`、`services/VFXComposer.Broker/Queries/ReadOnlyQueryRouter.cs`、`services/VFXComposer.Broker/Registration/ProjectRegistrationStore.cs`、`services/VFXComposer.Broker.ServiceHost/Program.cs`、`services/VFXComposer.Broker.ServiceHost/VFXComposer.Broker.ServiceHost.csproj`、`services/VFXComposer.Broker.ServiceHost/WindowsScmServiceHost.cs`、`services/VFXComposer.Broker.Tests/ProductionReadConvergenceTests.cs`、`services/VFXComposer.Broker.ServiceHost.Tests/WindowsScmServiceHostTests.cs`、`services/VFXComposer.Broker.ServiceHost.Tests/ProductionRunningGateTests.cs`。 | replay every listed predecessor receipt **and the I1 privileged live-store receipt**; D1R core receipt alone is insufficient. Require first serving P1 receipt, exact live C2 locator/locator-ACK, Job receipt and enrollment before `Running`/accept; Broker performs zero content I/O; `process-image/1` never sole proof. Any missing predecessor leaves `W24FS001`/`23`. |
| D2 `WP-P2-CLIENT-DESKTOP-PRODUCTION-READ` | B1 + C1 | Broker-only Client/Desktop read connection/status：`src/VFXComposer.Client/NamedPipeVfxComposerConnection.cs`、`src/VFXComposer.Client/ProjectRegistrationClient.cs`、`src/VFXComposer.Client/ReadOnlyProjectQueryClient.cs`、`apps/VFXComposer.Desktop/Services/ProductionProjectConnectionService.cs`、`src/VFXComposer.Client.Tests/ProductionNamedPipeVfxComposerConnectionTests.cs`、`apps/VFXComposer.Desktop.Tests/ProductionProjectConnectionTests.cs`。 | no-path selection, disconnected/revoke/recovery and zero direct project-I/O gates。no UI authority claim or Desktop→Worker fallback。 |
| E1 `WP-P2-INSTALLED-PRODUCTION-READ-E2E` | D2 | clean installed-system production-read receipt：`tests/EndToEnd/VFXComposer.ProductionRead.E2ETests.csproj`、**the test-project lock** `tests/EndToEnd/packages.lock.json`、`tests/EndToEnd/InstalledProductionReadTests.cs`、`tests/EndToEnd/InstalledProductionReadRecoveryTests.cs`、`build/Broker/verify-production-read.ps1`。 | install→launch→first/later serving ACL receipts→dedicated Worker→registered project allow-list read→disconnect/crash recovery，with no test artifacts or residual service/project mutation。any unbound receipt STOP。 |
| A2 `WP-P2-PRODUCTION-READ-FINAL-AUDIT` | E1 | independent frozen-byte/source/receipt/provenance audit：exact owned handoff `docs/coordination/handoffs/WP-P2-PRODUCTION-READ-FINAL-AUDIT.md`，以及唯一 generated/read-only receipt-root exception `.codex_tmp/WP-P2-PRODUCTION-READ-FINAL-AUDIT/**`。该 exception 不是 author-controlled exact source ownership，不授权任何未列 source path，也不预设具体 future receipt 文件名。 | P0/P1 must be zero, adversarial matrix replayed, claims/status/provenance/unique-leaf and sequential-overlay ledger checked；no source edits。audit ambiguity is NO-GO。 |

The rebased edges are exactly: `D1R→I1→A1`, `D1R→P1`, `D1R→S1`,
`I1→R1`, `C2→W1`, `C1+C2+W1+A1+P1+S1+R1→B1`,
`B1+C1→D2`, and `D2→E1→A2`. They form 8 edge groups and 17 directed
edges; a topological order exists with C1/C2/D1R before their consumers and
there is no D1R live-acceptance edge back from I1. A1 still supplies only a
receipt primitive; it cannot decide ServiceHost `Running`. B1 owns the only
final Running guard and must consume I1's privileged live-store receipt rather
than a dormant D1R receipt. No node may skip an edge via a test peer, caller
path, legacy Unity run, in-memory profile, current-user pipe setting, grant
ACK or unowned process.

The owner table and this edge list use the same dependency identity: P1 and S1
depend on **D1R**, never the retained historical `D1_0` checkpoint or old
`D1`. `D1_0`/`D1` names remain STOPPED/NO-GO provenance only and are not
13-node graph prerequisites.

### 9.1 Exact sequential-overlay ledger

The C1→C2 overlay may start only after the C1 closeout is frozen. C2 must
replay every listed SHA-256 immediately before its first write; a fresh audit
must replay the final C2 bytes. D1_0 is preserved byte-for-byte as a
STOPPED/NO-GO checkpoint. D1R may start only after a new writer verifies the
three D1_0 prior hashes; it must not alter a fourth product/test file or reuse
the retired D1 writer/auditor.

| Sequential owner transition | Exact path | Required prior SHA-256 |
|---|---|---|
| C1 → C2 | `src/VFXComposer.Protocol/Json/StrictWireCodec.cs` | `727bb2c30dc7ba13fbb1425277942a04b1d67fc3d0053b6a80a2f48cf11190f7` |
| C1 → C2 | `src/VFXComposer.Protocol/MessageKinds.cs` | `ef9d472566288ba926992a7b893ca3d4e837286abebb8a2bf905886588d2edde` |
| C1 → C2 | `src/VFXComposer.Protocol/WireSchemaRegistry.cs` | `9ca4a574e9631045240bd9cd07869fa9c9c3bb8910b8d6927f51977894b77934` |
| C1 → C2 | `src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs` | `1240071df65e980a2db27d66d7acebaa7958626e7a27dd9438c96ca392e74c70` |
| C1 → C2 | `src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs` | `5e7c8f80064c931151aea189cf6fe4224e5fe4d23c3308b1018f4d2ea48105b9` |
| C1 → C2 | `src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs` | `2acdb05f0cecc51476fed9997542c9b698fbd3a4eb4421f47d310ef939c0aa3d` |
| C1 → C2 | `src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs` | `0a8b0781e25607aec6976338673d13971970478b139b48ec6b0199ba1e6ead74` |
| C1 → C2 | `src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs` | `6ce16811d756c279e916c14773ad398aa0078be9791c67b6ff7d33596705641b` |
| C1 → C2 | `eng/verify-phase2-schemas.py` | `5f58482d48480fa55eba031e87177be283f9f3ccdd66b24af391d6057a4ee320` |
| D1_0 → D1R | `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs` | `f253647c8e24611d1908113b3d2c22eee1d5964ce915debc1efc6a885cc47731` |
| D1_0 → D1R | `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs` | `abbbe04b3e48c7dc5d0dec656df28174db19107d3edfc663d97c3c33a847fb32` |
| D1_0 → D1R | `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs` | `2fff247e51a9ee5b282c343ae0af72fe3fb78c55db59c379096e42758fb300d7` |

The first nine rows are exactly C2's shared Protocol/codec/test/verifier
overlays. The last three are exactly D1R's remediation overlays. They are
intentional sequential reuse, not ownership duplicates. C2's seven other
paths are unique new leaves. No C2/D1R overlay can be active concurrently
with another writer of the same row, and no overlay hash may be rebaselined
without a new ADR and independent audit.

### 9.2 C2 locator and acknowledgement contract

C2 defines capability `worker.project-locator.v1`, locator kind
`worker.project.locator`, and acknowledgement kind
`worker.project.locator.ack`. They are separate from
`worker.handle-lifecycle.v1`, `worker.project.handle.grant.ack`, and every
handle/revoke acknowledgement kind.

The immutable locator has exactly these required wire properties:
`protocolVersion`, `messageKind`, `requestId`, `registeredProjectId`,
`projectIdentity` (typed `vfxcomposer.project-identity/1`),
`volumeIdentity` (typed `vfxcomposer.volume-identity/1`),
`repositoryIdentity` and `projectRootIdentity` (each typed
`vfxcomposer.directory-identity/1`), positive Int64 `brokerGeneration`,
`registrationGeneration`, and `enrollmentGeneration`, bounded
`workerSessionId` and `workerProcessEpoch`, and a typed
`vfxcomposer.worker-project-locator/1` `selfHash`. These are immutable
host-owned correlations, not a caller-selected location.

The immutable locator acknowledgement has exactly
`protocolVersion`, `messageKind`, `requestId`, `registeredProjectId`,
positive `brokerGeneration`, `registrationGeneration`, and
`enrollmentGeneration`, `workerSessionId`, `workerProcessEpoch`,
`locatorSelfHash` typed as `vfxcomposer.worker-project-locator/1`, the
sole disposition `LOCATOR_ACCEPTED`, and typed
`vfxcomposer.worker-project-locator-ack/1` `selfHash`. It acknowledges
only this exact locator correlation. It contains no path, URI, drive,
Volume-GUID text, directory text, raw/native handle, lease ID, handle grant,
permission, authority, command, status, accepted Boolean or session issuance.

Both schemas use exact required sets and `additionalProperties:false`.
Strict .NET tests, schema verifier positives/negatives, and the canonical C2
golden-vector bytes must reject BOM, decoded duplicates, unknown/missing/wrong
types, wrong kind/version/domain, nonpositive generations, cross-session/
cross-epoch/cross-generation/self-hash drift, caller-path-shaped fields and
authority-shaped fields. C2's immutable vector is the same vector W1 must
replay in its Unity strict decoder/encoder test; C2 creates no Unity source,
pipe, process, locator issuer, project read or ACK issuer.

### 9.2.1 Narrow ADR-003 ownership supersession for the W1 composition shim

Only for the nine exact W1 paths in the owner table above, ADR-003 §4's
ownership sentence **`Unity 侧只允许新增：`** and its immediately following
two-path listing are narrowly superseded. The supersession is only a location
exception for this fixed composition shim; it does not authorize an additional
adapter directory, a tenth W1 path, a directory-wide grant, or any change to
ADR-003 itself.

No other ADR-003 decision or ownership rule is superseded. In particular,
ADR-003 §2's single narrow Unity adapter remains the only normative Unity wire
implementation. The existing ADR-003 `Worker/Protocol/**` files, including
`project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Protocol/W24S6WorkerProtocolCodec.cs`,
and `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerProtocolTests.cs`
are frozen read-only dependencies. W1 may only compose/reuse that codec's
canonical primitives and C2's exact schemas and canonical golden bytes. It may
not declare, copy, alter or independently implement DTOs, hash types or
functions, canonicalization, token/schema registry entries, message kinds,
schemas, golden bytes, encoders or decoders.

The exact planned W1 test file may add composition coverage only by replaying
the frozen ADR-003 adapter and the immutable C2 vectors byte-for-byte. The
historically named production `WireCodec` file is not a codec authority and may
not become a second parser, sealer or canonicalizer. This preserves one
normative adapter and prohibits a protocol fork.

### 9.3 D1R dormant core and I1 privileged integration split

D1_0 remains a current three-source NO-GO checkpoint. D1R must repair source
and static/adversarial tests for internal store-file `ACCESS_SYSTEM_SECURITY`
opens, pending rename `DELETE` and target-directory rights, native secret
zeroing, distinct root/file least-privilege descriptors, exact pending-suffix
rollback and ordinal manifests. Its scoped core GO, if independently audited,
proves only that the remediation remains dormant and fails closed when its
privileged prerequisite is absent. It cannot report a successful strict store
open, commit, reopen or readback.

I1, not D1R, provisions and pins the local-NTFS root, enables
`SeSecurityPrivilege` in the executing token, supplies the root/store
`ACCESS_SYSTEM_SECURITY` rights and target-directory rename rights, then
performs the first real strict D1R store commit/reopen/readback. Its live-store
receipt must bind the pinned root identity, enabled privilege state, requested
rights, D1R hashes, durable profile/generation and exact result. B1 must
consume and replay that I1 receipt; D1R's dormant-core receipt is never a
substitute.

## 10. Gate and evidence rules

Before B1, all `Program`/`BrokerPolicy` real-entry tests must prove exact
`W24FS001`/`23` before listener/path/project I/O. B1 is eligible only when
the predecessor manifests, source hashes, test receipts, installation/readback
receipts and claim-kind records all match the same durable profile/generation.
Its final ServiceHost guard must require the first **serving** P1 receipt—not a
bootstrap receipt—before `Running`, and must require every later serving
instance to replay its own apply/readback receipt before accept.
Each subsequent node must bind source bytes, build/test results, service/Worker
process facts and install-system state into new evidence; none may rebind
r31/r32/r35/r36, test pipes, HandleProbe, test issuers, scratch projects or
earlier dormant-policy receipts.

The Phase 2 gate is not complete merely because B1 or D2 passes. Only E1 plus
A2 can propose `PHASE_2_PRODUCTION_READ_GO`, and that conclusion remains
strictly read-only: it does not grant command mutation, preview, visual pass,
user verdict, L3 or L4.

## 11. Consequences and explicit non-goals

- The ordinary Windows product becomes more honest: it can prove a protected,
  launch-correlated installation object while explicitly preserving
  `LoadedImageVerified=false`.
- A strict loaded-image/backing-file or memory-integrity product is deferred
  to a high-assurance, separately authorized platform-security design.
- Unity remains mandatory for project content and Unity API, but production
  uses a dedicated supervised Worker rather than the user's interactive Editor.
- Desktop gains no filesystem privilege and no authority issuer.
- Existing dormant policy, service-host, path observation and pinned-handle
  observation packages stay valid only within their stated scopes; this ADR
  neither backfills them into production nor deletes them.

Any change to the four-claim taxonomy, ordinary-profile
`LoadedImageVerified=false` state, dedicated Worker rule, host-owned project
locator, frozen DAG or B1 activation ordering requires a new ADR plus an
independent P0/P1 audit.
