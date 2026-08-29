# VFX Composer：项目总体架构与开发总设计

> **当前最终状态（2026-08-29）**：普通用户本地创作路线 <code>USER_MODE_LOCAL_CREATIVE_TOOL_V1</code> 已在 U6 以 <code>FINAL GO</code> 关闭，完成度 <code>100/100</code>；AI 双通道路由 <code>AI_PROVIDER_TWO_CHANNEL_ROUTING_V1</code> 已在 A6 以 <code>FINAL ACCEPTED/GO</code> 关闭，完成度 <code>100/100</code>。两条路线的最终独立验收均为 <code>P0/P1/P2=0/0/0</code>。没有活动中的 U/A 工作包。
>
> 本文是全项目的单文件阅读入口：它解释当前产品边界、实现架构、开发决策、闭环证据和下一里程碑的启动条件。它不是把旧计划拼接成目录；历史材料只保留为可追溯的决策背景，不能覆盖本文件所指向的最终验收状态。

## 1. 文档目的、范围与权威优先级

### 1.1 目的

VFX Composer 是面向当前 Windows 登录用户的本地创作工具。其完成的两项主线是：

1. 让 Desktop 经由普通用户模式的 Broker/Worker，针对用户**明确选择**的 Unity 项目完成受限、只读的资料读取；
2. 在 Desktop 内提供彼此隔离的文本与图像 AI 通道，使用户能显式配置提供方，同时保持无隐式路由、无自动联网、无项目写入和无敏感数据泄露。

本文将这两条主线置于一个统一的进程、模块、信任和证据模型中，并明确哪些能力已经证明、哪些能力从未宣称。

### 1.2 权威读取顺序

发生表述不一致时，按以下顺序解释：

| 优先级 | 资料 | 作用 |
|---|---|---|
| 1 | <code>docs/coordination/W24_EVIDENCE_INDEX.md</code> 的 “CURRENT EVIDENCE ROUTING” 与 U6/A6 关闭段 | 当前最终验收状态、receipt、数字和不作声明的边界。 |
| 2 | <code>docs/coordination/W24_PROGRAM_CONTROL.md</code>、<code>docs/coordination/W24_WORK_PACKAGE_REGISTRY.md</code> 的 current 段 | 当前控制状态、依赖、所有权、复用和停用规则。 |
| 3 | <code>docs/rules/ADR-006_AI_PROVIDER_TWO_CHANNEL_ROUTING.md</code> | AI 双通道的规范设计与最终接受范围。 |
| 4 | <code>docs/rules/ADR-005_USER_MODE_BROKER_WORKER_ARCHITECTURE.md</code> | 普通用户 Broker/Worker 的规范结构、威胁模型和 U DAG。其 U0 时期的 “尚未 U5/U6” 门槛已被 U6 最终 receipt 取代。 |
| 5 | <code>docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md</code>、<code>docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md</code> 的 current closeout | 阶段计划与实际交付的当前摘要。 |
| 6 | <code>docs/stage-notes/POST_U6_AI_PROVIDER_TWO_CHANNEL_PLAN.md</code> | A0 时的计划性输入；其中 A1 active/A2–A6 not started 等状态已由 ADR-006、控制文档和 A6 receipt **完全 superseded**。 |
| 7 | 各程序集、测试、schema、gate runner 源码 | 实现导航和行为交叉核验；不能单独替代独立验收 receipt。 |

所有标有 “Historical pre-U0”、旧 ADR-004、D1/D1R、ServiceHost、SCM/install、I1/R1/A1/B1，或早期被拒绝 writer 的材料均是**历史 provenance**。它们既不是当前缺陷，也不是 U0–U6/A0–A6 的依赖或 blocker。旧材料中的 “NO-GO”、active package、未开始节点，不能被误读为当前项目状态。

### 1.3 当前闭环摘要

| 范围 | 当前状态 | 控制性结论 |
|---|---|---|
| USER_MODE | U0–U6 全部关闭，U6 <code>P0/P1/P2=0/0/0</code>，<code>100/100</code> | Desktop/Client -> Broker -> standalone Worker 的本地、当前用户、显式项目选择、只读资料读取路线完成。 |
| AI provider | A0–A6 全部关闭，A6 <code>P0/P1/P2=0/0/0</code>，<code>100/100</code> | 所有文本走 <code>ChatLlm</code>，所有图像生成走 <code>ImageGeneration</code>；每通道只有一个显式绑定，零 fallback。 |
| 默认 Broker 表面 | 保持 fail-closed | 无参数启动必须为空 stdout、stderr 精确为 <code>W24FS001</code>、退出码 <code>23</code>。这不是网络、AI 或 Unity 写入能力。 |
| 后续工作 | 无活动包 | 任意新增功能必须开启新的里程碑；不得把已关闭节点改写为进行中，或静默扩展其范围。 |

## 2. 产品目标与明确非目标

### 2.1 已交付的产品目标

- 为当前登录用户提供 Avalonia Desktop 创作界面：Dashboard、Library、Create、Preview、Patch、Review、Jobs、Settings。
- 仅在用户明确选择有效 Unity 项目之后，建立受限的当前用户 Broker/Worker 会话，并读取限定资料；Desktop 本身不读写 Unity 项目文件。
- 将 Unity API 与项目内容操作保留给 Worker/Unity 侧；Broker 只做进程关联、会话准入、项目绑定和转发。
- 为文本/对话和图像生成提供两个完全独立、显式、可审计的 AI 通道。
- 将提供方配置保存在当前用户应用数据中，采用原子 JSON、版本/修订号、<code>.bak</code> 恢复、DPAPI 当前用户密钥保护和 redaction。
- 使用户配置的 endpoint 能原样保存为有界文本；仅在一次显式请求构造时由对应 adapter 解释。
- 对 AI 返回的图片采用私有、非受信任、非项目的缓存与内存预览模型。

### 2.2 不属于已交付结论的能力

- 没有 Windows Service、SCM 注册/安装、<code>LocalSystem</code>、特权 installer、特权 enrollment、<code>SeSecurityPrivilege</code>、<code>SeRestorePrivilege</code>、严格 SACL live gate、loaded-image proof 或特权 issuer。
- 没有任意路径、任意项目、任意 caller path、共享机器 endpoint、公开 HTTP/TCP 管理入口或环境变量信任根。
- 没有 Desktop 直接访问 Unity 项目文件、Desktop 直接连接 Worker、AI 将内容自动写入 <code>Assets</code>、配方、patch 或任何 Unity 项目路径。
- 没有同用户恶意代码对抗、管理员/内核/离线篡改/调试注入防护的安全声明。
- 没有提供真实付费账号、真实生产 credential、真实外部付费调用、生产发布或云服务可用性的验收声明。
- 没有自动 health probe、自动 DNS/HTTP、自动图片生成、自动 provider 推断、cookie/browser 状态提取、CLI/sidecar、动态 provider DLL、任意 header 模板或 TLS 验证绕过。
- 没有 AI provider fallback；重试若将来出现，也只能针对已经确定的同一条 route，不能换通道、profile、capability、model、endpoint、protocol 或 adapter。

## 3. USER_MODE_LOCAL_CREATIVE_TOOL_V1：威胁模型与信任边界

### 3.1 信任前提

该产品有意采用“单用户、局部、显式创作”的模型，信任：

1. 当前登录的 Windows 用户；
2. 该用户主动启动的 Desktop、Broker、Worker 或 Unity host；
3. 该用户明确选择的 Unity 项目；
4. 经普通发行完整性检查得到的本地 release bytes。

hash/signature 只证明 release/update 完整性；它们不会把进程、机器结果、视觉结果、用户 verdict 或项目选择提升为 authority。

### 3.2 必须 fail-closed 的攻击/故障类

| 类别 | 设计反应 |
|---|---|
| 其他 Windows 用户连接本地 pipe | 使用 <code>CurrentUserOnly</code> / 当前用户 SID 限制；不接受跨用户 peer。 |
| 重放、旧会话、跨 generation 消息 | 绑定随机 pipe、一次性 nonce、session、单调 generation、角色和协议版本；变化后全部失效。 |
| PID 复用或假 child | PID 不是身份；验证父子关系、保留 process handle、creation-time epoch、预期 image layout 和活动状态。 |
| 非法项目、父/兄弟目录、UNC/device/ADS、reparse 逃逸 | 仅允许显式选择的 canonical local root；Broker 与 Worker 双重校验 containment 与 Unity markers。 |
| protocol/schema drift、未知字段/消息、partial frame | 严格 codec/长度/UTF-8/消息类型校验，在 project I/O 前拒绝。 |
| 崩溃、断线、重启、孤儿 child | session/revoke/Job/child handle 的清理链；失败转 RecoveryRequired，重新建立 generation。 |
| endpoint、credential、pipe、nonce、路径泄露 | 稳定错误码和 redacted diagnostics；不记录 raw endpoint、secret、Authorization、prompt、image bytes 或受限 locator。 |

### 3.3 明确不防御的对象

恶意同用户代码、同用户 debugger/injection、local administrator、kernel compromise、离线磁盘篡改、以及用户故意选取的恶意项目都在模型之外。因此不能把 <code>CurrentUserOnly</code> 描述成对同用户恶意程序的隔离，也不能将 U5 的静态/IL 跨用户证据夸大成多帐户 E2E。

## 4. 总体架构：程序集、进程、IPC 与数据流

### 4.1 逻辑与进程图

~~~mermaid
flowchart LR
  UI["Avalonia Desktop UI<br/>apps/VFXComposer.Desktop"]
  Client["Client session host<br/>src/VFXComposer.Client"]
  Broker["Broker child<br/>services/VFXComposer.Broker"]
  Worker["Unity Worker child<br/>services/VFXComposer.UnityWorker"]
  Unity["Selected Unity project<br/>bounded read only"]
  Contracts["Protocol + AI contracts"]
  Settings["Current-user AI settings<br/>JSON/.bak + DPAPI"]
  Chat["Chat adapter<br/>one ChatLlm route"]
  Image["Image adapter/cache<br/>one ImageGeneration route"]
  Upstream["Explicit user-selected upstream"]

  UI --> Client
  Client -->|"current-user pipe, bootstrap nonce"| Broker
  Broker -->|"current-user pipe, C2 locator + ACK"| Worker
  Worker -->|"bounded LibraryIndex / manifest read"| Unity
  UI -->|"IAiDesktopRuntime / IAiGateway"| Settings
  Settings --> Chat
  Settings --> Image
  Chat -->|"explicit prompt only"| Upstream
  Image -->|"explicit generate only"| Upstream
  Contracts --- Client
  Contracts --- Broker
  Contracts --- Worker
  Contracts --- Chat
  Contracts --- Image
~~~

关键隔离是：Desktop 不对 Unity project 做直接 filesystem I/O；Broker/Worker/Unity 不持有 AI provider config、secret 或网络职责；AI image artifact 不自动回写项目；feature caller 不可选择 route。

### 4.2 程序集与目录责任

| 位置 | 责任 | 关键禁止项 |
|---|---|---|
| <code>src/VFXComposer.Protocol</code> | 严格 wire DTO、codec、状态、locator/ACK、无副作用协议。 | 不做 project I/O、AI、UI、secret。 |
| <code>src/VFXComposer.Client</code> | Desktop 与 Broker 的 child process host、session 状态机、recovery/restart。 | 不做 Desktop project filesystem I/O，也不直接到 Worker。 |
| <code>services/VFXComposer.Broker</code> | Desktop/Broker admission、child correlation、项目选择、locator 发放、Worker routing/revoke。 | 默认无参数不得启动 listener；不做 Unity API、AI transport 或 project content mutation。 |
| <code>services/VFXComposer.UnityWorker</code> | 接收 Broker bootstrap/locator、验证 working directory 与 identity、执行受限只读。 | 不接受任意 path；不写项目；不持有 provider secret/config。 |
| <code>project/Packages/com.vfxcomposer.unity</code> | Unity-side adapter、编辑器和 EditMode 约束测试。 | 不改变 Desktop/Broker 的 authority model。 |
| <code>apps/VFXComposer.Desktop</code> | Avalonia UI、项目选择 dialog、AI Create/Preview/Settings。 | 不直接读写选中项目；不直接构造 provider transport route。 |
| <code>src/VFXComposer.AI.Contracts</code> | <code>OpaqueEndpoint</code>、profile/capability/binding、channel DTO、<code>IAiGateway</code>、desktop contract。 | 不含 HTTP、Desktop UI、Broker、Worker、Unity、secret payload。 |
| <code>src/VFXComposer.AI.Providers</code> | config/DPAPI/redaction/resolver、Chat/Image adapters、私有图片 cache、desktop runtime/settings。 | 不赋予项目写入或 Broker/Worker 角色；A1 基础层不因保存/解析而联网。 |
| <code>src/VFXComposer.AI.Tests</code>、<code>tests/VFXComposer.LocalE2E.Tests</code>、<code>tests/VFXComposer.AiLocalE2E.Tests</code> | 合约、边界、普通用户 E2E、loopback provider E2E。 | 不使用真实 secret、外部/付费 provider 或 project mutation。 |
| <code>eng</code>、<code>docs/schemas/desktop</code> | unified gate、offline restore、schema 校验、baseline/root/residue receipt。 | 不作为运行时 authority。 |

### 4.3 Desktop、Broker、Worker、Unity 的职责分配

| 主体 | 可以做什么 | 必须不能做什么 |
|---|---|---|
| Desktop | 显示状态；要求用户选择目录；调用 Client session 的 Connect/Select/Read/Restart；调用 Gateway/Settings；内存预览私有图片。 | 不自行验证/读取项目内容；不将 raw path 发到 Worker；不直接 provider transport；不自动联网。 |
| Client | 创建 Broker child、向它传递一次性 bootstrap、校验 peer、串行化 session 状态。 | 不变成项目或 AI 权限代理。 |
| Broker | 校验 Desktop child/pipe；在选择后启动 Worker；选择根、签发 scope-bound locator、核验 ACK、路由 read、revoke/cleanup。 | 不执行 Unity API；不接收任意文件路径；无 args 时不服务；不保存 AI secret。 |
| Worker | 接受被绑定的 locator，核验自身进程/session/generation/root identity；读取允许的 library index 或 manifest 并返回内容 hash。 | 不接受 caller path；不扩展到任意项目文件；不写项目；不持有 AI route。 |
| Unity | 保有 Unity API 和项目内容语义；提供 Worker-side adapter 及其测试。 | 不替代 Broker 的 admission 或 Desktop 的 UI/AI settings。 |

## 5. 普通用户 session、nonce、generation、PID/epoch 与 cleanup

### 5.1 生命周期

1. Desktop 创建 <code>UserModeDesktopSession</code>，状态从 <code>Disconnected</code> 经 <code>Starting</code> 到 <code>ConnectedNoProject</code>。
2. Client 只从 release layout 解析精确的 <code>VFXComposer.Broker.exe</code>，创建 current-user Desktop pipe、随机 session/nonce，并以 <code>--user-mode-desktop-child</code> 启动 Broker；Desktop 保留 child handle/Job。
3. Broker 从 stdin 接收 bootstrap，连接受限 pipe，双方通过 pid、epoch、generation、session、nonce、role 和协议完成 Hello。nonce 被消费一次。
4. 此时尚无 Worker。只有用户明确 Select 通过 root validation 后，Broker 才从固定 release 路径启动 <code>VFXComposer.UnityWorker.exe</code>，并将选中 canonical root 作为其 working directory。
5. Broker 与 Worker 建立独立 current-user pipe/bootstrap；Worker 必须严格接受 C2 locator 并回送 ACK，Broker 才向 Desktop 返回 <code>SelectAccepted</code>。
6. Read 只在当前 lease/session/generation 可用时执行；消息关联 id、project identity、document id 和 response 都必须一致。
7. 任一 disconnect、parent death、Worker restart、reselect、generation advance、ACK/read error 或协议不一致都会 revoke 当前 binding，销毁旧 Worker/session，Desktop 进入 <code>RecoveryRequired</code> 或显式 Restart。

### 5.2 关键不可替代的关联量

| 量 | 用途 |
|---|---|
| 随机 pipe name | 本地不可猜测名字；不作为唯一认证。 |
| session id | 将一次连接的控制/读取消息绑定到同一生命周期。 |
| one-use nonce | 阻断 bootstrap/hello 重放；每次重连、崩溃、restart 都重新生成。 |
| monotonic generation | 使旧 message、旧 lease、旧 locator 在新会话中失败。 |
| PID + retained process handle + creation epoch | 处理 PID reuse；文本 PID 永远不单独充当身份。 |
| protocol version / role | 阻断 protocol drift、错误端点或跨角色连接。 |
| selected project identity / locator hash | 将读取限定到同一用户选择的 root，而不是 caller 提交的 path。 |

实现中，Desktop-side codec 使用 <code>UDB1</code>/<code>UDM1</code> framing；Worker bootstrap peer 使用 <code>UMB1</code>/<code>UMH1</code>。<code>UserModeDesktopSessionCodec</code> 对长度、严格 UTF-8、message kind 和 payload 上限（1 MiB）进行校验。nonce/payload 在失败或完成后清零，日志仅保留稳定码（例如 <code>U4FS001</code>），不输出随机值或 unrestricted path。

### 5.3 清理与失败语义

<code>UserModeChildProcess</code> 在恢复 child 前绑定 kill-on-close Job，保存 process handle/epoch，检查预期 release image path；<code>UserModeNamedPipeServer</code> 在 accept 时重查 peer PID 与 child handle/epoch。Broker 的 <code>Invalidate...</code> 路径先 revoke lease，再 dispose Worker/session。Client dispose 同样关闭 pipe/Job、终止或等待 child。

这是一套普通用户生命周期 containment，而非 sandbox，也不是同用户恶意对抗声明。

## 6. 项目选择、locator、ACK 与只读边界

### 6.1 选择是唯一的项目意图来源

项目只能由 Desktop（或 active Unity host）中的显式用户动作引入。<code>UserModeProjectSelectionStore</code> 在启动 Worker 前核验：

- root 是存在的本地 canonical directory；
- 不是 UNC、device path、ADS/异常 colon 形式，且祖先中无 reparse escape；
- 具备 Unity markers：<code>Assets</code>、<code>Packages/manifest.json</code>、<code>ProjectSettings/ProjectVersion.txt</code>；
- 重新选择会撤销旧 selection/lease 并推进 generation。

Desktop 将用户选择的目录字符串交给 Client/Broker session；它本身不使用 <code>File</code>/<code>Directory</code> 去读取项目。

### 6.2 C2 restricted locator 与 ACK

<code>WorkerProjectLocator</code> 不是任意路径 capability。它携带受限、关联的：

- protocol/version/kind/request id；
- registered project、project/volume/repository/project-root typed identities；
- Broker/registration/enrollment generation；
- Worker session/process epoch；
- locator self hash。

它不创建新的 handle、lease、session 或 authority。Worker 从 Broker 指定的 working directory 再次验证 root 与 identity，仅在关联完全一致时发出唯一的 <code>LOCATOR_ACCEPTED</code> ACK。Broker 在收到精确 ACK **之前**不得发送 <code>SelectAccepted</code>。

### 6.3 读取范围

<code>UserModeProjectReadSession</code> 与 Worker 的 <code>ReadDocumentQuery</code> 都是 path-free、严格关联的请求。当前只允许受限的：

- <code>LIBRARY_INDEX</code>，映射至 <code>ProjectSettings/VFXComposer/LibraryIndex.json</code>；
- 有效 manifest id，对应 <code>ProjectSettings/VFXComposer/BuildManifests/{id}.manifest.json</code>。

Worker 继续执行 root containment、reparse、大小、严格 JSON object、内容 hash 等检查。它不接受兄弟/父目录、任意 manifest path、UNC/device/ADS 或后续 caller 自带 path。该产品路径是只读；不承诺 command、mutation、evidence、verdict 或 authority。

## 7. AI provider：两个显式、独立、无 fallback 的通道

### 7.1 通道与 caller contract

| 通道 | 唯一允许业务 | 强制绑定 |
|---|---|---|
| <code>ChatLlm</code> | 全部 LLM、conversation、text-generation。 | 唯一显式 <code>ChatLlm</code> binding。 |
| <code>ImageGeneration</code> | 全部 image-generation。 | 唯一显式 <code>ImageGeneration</code> binding。 |

feature caller 只能用 <code>IAiGateway</code> 的 channel-specific DTO（例如 Create 调用 Chat、Preview 调用 GenerateImage）。它不能选择 profile、model、protocol、endpoint、adapter、header、credential 或“第二选择”。每个 binding 精确指向同通道的：

<code>ProviderProfile + SecretRef + CapabilityDefinition + model + ProtocolBinding</code>。

<code>ProviderOrigin</code>（<code>Official</code>、<code>Relay</code>、<code>Friend</code>、<code>Subscription</code>、<code>Custom</code>）只描述来源；它不能推断 protocol、认证方式、adapter、模型或 fallback。缺失、disabled、unknown、cross-channel、stale、corrupt、unsupported、secret unavailable 或 capability mismatch 都在网络前 fail closed。

### 7.2 配置模型、原子修订与密钥

AI 配置属于当前用户应用数据，不是 Unity project config。核心 aggregate 是带版本和递增 revision 的 <code>AiProviderSettings</code>：

- profiles 以稳定 id 唯一化，包含 display name、origin、enabled、protocol、<code>OpaqueEndpoint</code>、timeout、capabilities 和 <code>AuthDescriptor(SecretRef)</code>；
- 每个 channel 最多一个 <code>ChannelBinding</code>，其 profile/capability/model 必须精确匹配；
- <code>ProviderConfigurationStore</code> 在一把持久 <code>.lock</code> 上完成“读取 -> revision 检查 -> validate -> 原子 replace”，避免 read-modify-write race；
- JSON 以有界 UTF-8 写入，保留 <code>.bak</code>；primary 损坏时仅恢复已验证 backup；primary/backup 都无效或版本不支持时 fail closed；
- revision lock 通过 <code>FileShare.None</code> 的 durable anchor 加 OS-level lease，锁不会以“删除后再创建”方式产生竞态；
- 每次有效配置变更生成 fingerprint，使旧 health observation 变为 stale。

secret 只以 <code>SecretRef</code> 引用。<code>ProviderSecretStore</code> 使用 Windows DPAPI <code>CurrentUser</code> 及产品版本化 purpose/entropy。新的 secret 先存入新的 profile-bound reference，再提交配置；保存失败会撤销新 orphan，不破坏旧可用 reference。Revoke 会先把配置改成不可读的新 reference，再尽力清除旧 envelope，因此 route 仍然 fail closed。UI secret 是 entry-only，不能回读 plaintext。

### 7.3 OpaqueEndpoint：保存与解释必须分离

<code>OpaqueEndpoint</code> 是用户拥有的配置字符串，最大为 8 KiB UTF-8。它可以是合法 URI、带 scheme/host/port/user-info/path/query/fragment 的 URI，也可以是非 URI 文本；**空字符串和仅空白字符串也属于可保存值**。

在 JSON/codec、schema、atomic store、resolver、import draft、Settings edit 之间，值必须按用户输入原样 round-trip。允许的本地约束只有 aggregate/type/version/revision/duplicate/unknown-field/size 等结构性规则。下列行为均被禁止：

- 把 URI parse、scheme/host/port 校验、trim、normalize、repair 或 upstream reachability 当成保存/解析准入条件；
- 自动重写、保存 canonical URI、以成功/失败结果替换配置；
- 依据 URL、origin、model、display name、环境或“唯一 enabled”推导另一条 route。

只有 A2 Chat 与 A3 Image adapter 能在**一次已确定的显式请求**构造期间把原始字符串交给 .NET/HTTP 形成 transient <code>RequestUri</code>。若无法形成 HTTP/HTTPS request URI，返回稳定、redacted 的 endpoint/request-construction failure；如果能形成，则该值就是完整 target，adapter 不得追加 vendor path、拼接 query 或持久化 request-time normalization。成功和失败都不能回写 config、disable/delete profile 或选另一条 route。

endpoint 中可能含 user-info/query credential，因而它是敏感值；但它也**不是** <code>SecretRef</code> 的替代品。正式认证仍应使用 DPAPI/SecretRef。

### 7.4 Health、导入、导出与 observability

- 保存设置、应用启动、页面导航（Create/Settings/Preview）是 zero-network：不得 parse/probe endpoint、DNS、创建 HTTP client、refresh credential、下载图片或做 health check。
- health 初始为 <code>Unknown</code>，不阻止一次用户明确的 prompt；该请求才是第一条网络请求并记录该 route 的观测。没有自动 Chat/Image health probe。
- Image 必须由明确 Generate 动作触发；状态展示、选择、保存、启动或导航不能触发付费/网络请求。
- 日志、异常、receipt、telemetry、normal UI、cache key、默认 export 必须只显示 redacted endpoint summary 与稳定码，不能泄漏 raw endpoint、Authorization、token、SecretRef payload、prompt、raw response 或 image bytes。
- raw endpoint 只能在用户刻意打开的 profile edit surface 中显示，编辑结束应清空 transient UI 字段。普通列表只显示 redacted summary。
- export 必须明确选择 “include provider configuration” 并警告 raw endpoint 可能含凭据；默认不导出，且永不导出 secret payload。
- 固定 Tom 输入 <code>snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf</code> 仅用于用户确认的 draft import。<code>ApiKeyProtected</code> 不复制、不解密、不重加密、不返回、不记录、不持久化；import 不会自动激活 binding。

## 8. Chat 与 Image 的 HTTP/私有 artifact 边界

### 8.1 共同行为

两个 adapter 都只使用已 resolution 的**同一** route；没有 route search/fallback。production <code>HttpClientHandler</code> 一律：

- <code>AllowAutoRedirect=false</code>；
- <code>UseCookies=false</code>；
- 只接受已配置 complete endpoint；
- 使用 profile timeout、bounded request/response、稳定 redacted error；
- 不将 endpoint、secret、prompt、response 或图像原始内容写入 config/diagnostics。

redirect 绝不跟随，3xx 作为拒绝，而不是让 upstream 重新选择 host/route。

### 8.2 ChatLlm

<code>ChatChannelGateway</code> 在 explicit Chat prompt 前一次性 snapshot route，使并发保存新 revision 不影响 in-flight 调用的 profile/capability/model/protocol。它在请求时创建原始 endpoint 的 URI、创建 bounded payload、临时打开 DPAPI secret 并按明确 protocol 施加认证；无效 URI、网络、超时、HTTP rejection、过大或 malformed response 都映射为稳定 channel error，并把该 route 的 health 记录为封闭错误词表。

Chat response 有 1 MiB 有界读取；3xx 不解析/不发送 redirect target。没有 cookie、没有 provider discovery、没有 URL rewrite、没有将失败改路或将 endpoint 规范化写回。

### 8.3 ImageGeneration

<code>OpenAiCompatibleImageGateway</code> 对一个已解析的 Image route 生成请求。它以分离的 non-redirecting API/artifact client 处理 provider 的 base64 或 URL 返回：

- artifact URL 下载使用独立 client，**不转发 Authorization**；
- 3xx 被拒绝，避免 provider response 选择第二个 endpoint；
- 对 API/图片 body、credential、尺寸（最长边 4096、总像素 16M）、格式（PNG/JPEG/WebP）、MIME、hash 进行有界/一致性检查；
- 图像最大私有 payload 为 20 MiB，API response 亦有独立上限；
- 产物只返回 artifact id，不把 URL、path、bytes 或 prompt 暴露给 feature caller。

<code>PrivateImageArtifactCache</code> 是 provider-owned 的私有 temporary cache，不是 Unity export。它在 dispose/cleanup 中删除其 own root；A5/A6 receipt 对残留有专门检查。<code>PrivateImagePreviewDecoder</code> 的唯一例外是接收 provider-issued <code>Stream</code> 并直接解为内存 Avalonia <code>Bitmap</code>，立即关闭 stream（包括失败路径）。该 decoder 不得使用 <code>File</code>、<code>Directory</code>、<code>Path</code>、<code>FileStream</code>、<code>Environment</code>、<code>System.Net</code>、project path 或 Unity API。

## 9. Desktop UI 行为与用户可见安全语义

### 9.1 普通用户项目会话

MainWindow 显示 Connected/no project、Selected、Reading、RecoveryRequired 等状态。Connect 仅建立 Desktop -> Broker 普通用户会话；它不会选择项目、启动 Worker、读取项目或写项目。Select 打开 folder dialog 并把选择提交给 session；只有收到 C2 ACK 后 UI 才可显示 accepted selection。Read 只请求允许的 document；失败应呈现可恢复状态和稳定错误，不以隐藏重试绕过边界。底部语义是“read-only ordinary-user session；commands and mutation are disabled”。

### 9.2 AI Create、Preview、Settings

- **Create**：用户点击后才调用 <code>Gateway.ChatAsync</code>；无 preflight、无 fallback。
- **Preview**：用户点击 Generate 后才调用 <code>Gateway.GenerateImageAsync</code>；解码 private artifact 到内存 bitmap，并在替换/关闭时 dispose。
- **Settings**：可以 create/edit/save/delete profile、显式保存/清除 Chat 和 Image binding、输入或 revoke secret。profile summary 始终 redacted；raw endpoint 仅在 BeginProfileEdit 取得且只在 editor 生命周期展示；secret field 始终为空/密码 entry，保存/撤销后清空。
- 所有普通导航和 Settings CRUD 都不产生 provider 网络活动。UI 可显示 Unknown/Stale/Unavailable，但这些状态不能暗中触发 probe。

## 10. 交付 DAG 与每节点闭环

### 10.1 USER_MODE DAG

~~~mermaid
flowchart LR
  U0 --> U1
  U0 --> U2
  U1 --> U3
  U2 --> U3
  U3 --> U4 --> U5 --> U6
~~~

| 节点 | 目标与主要实现 | 关键提交/receipt | 测试、证据与结论 |
|---|---|---|---|
| U0 | 文档化普通用户 architecture rebase；固定七节点 DAG、威胁模型、复用/退休 ledger。无源码。 | <code>53c1eeb4577a7067d8702fdea9866adf01733191</code>。 | 仅七份文档的 docs-only 闭环；当时的 45/100、42%–48%、剩余 45%–55% shorter 是冻结计划基线，不是当前完成度。 |
| U1 | 合并 C3+W1，连接 C1/C2 到实际 Unity Worker connector，建立一套 canonical Unity adapter/peer ABI。 | <code>48cd27103f8fe0c510770b1584b326f55fca3485</code>。 | declared connector/codec/vector/schema/lifecycle negatives 完成；不单独证明 Desktop integration、项目 read、arbitrary path 或 authority。 |
| U2 | Broker/Worker ordinary-user child process、random pipe、nonce/session/generation、handle/epoch correlation、crash cleanup。 | <code>4b2f9a81a82911d68b8b64864ae05a03f9690b2e</code>。 | 初审记录 P1=3；一次 remediation 后三次 <code>42/42</code> 与 Broker <code>171/171</code> 关闭。该 remediation 不虚构第二次独立审计。 |
| U3 | 明确项目选择、restricted locator、Worker-only bounded read、reselect/revoke containment。 | source <code>0123616e21d656b2374809a13aeb2769f0324e7e</code>；merge <code>027ba07448dd6d4a0741a67937427cd2d37b2649</code>。 | 精确 7 文件；Broker <code>8/8</code>、Unity EditMode <code>9/9</code>、no-tests PASS、unified Broker <code>179/179</code>、manifest 前缀 <code>b716…</code>。只读/普通用户结论。 |
| U4 | Desktop 通过 U2/U3 建立 public session，显式 select/read 与 recovery UI；无 direct project I/O。 | source <code>2295b022348dc1514c72846533b86430bc4762ad</code>；merge <code>e1a6a9a37d3125717afbe795d283a07ffa242060</code>。 | Protocol <code>108/108</code>、Client <code>14/14</code>、Broker <code>183/183</code>、Desktop <code>12/12</code>；Release 0 warning/error；schema 22/13/14/236；smoke <code>W24FS001</code>/23；receipt manifest <code>b741fef9…</code>。 |
| U5 | 使用真实 public Desktop/Client -> Broker -> standalone Protocol-only Worker 做本地普通用户 E2E；验证 happy/adversarial/crash/recovery/cleanup。 | source <code>365e7612b1be276aa74f4ab36f40482a0858e1ae</code>；merge <code>b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d</code>；receipt <code>U5-local-e2e-independent-acceptance-20260828T144950082Z</code>。 | <code>P0/P1/P2=0/0/0</code>；精确 17 文件，Protocol 108/108、Client 16/16、Broker 183/183、LocalE2E 17/17；receipt 121 行，root replay 0，process/pipe/temp residue 0，assembly SHA-256/MVID binding。跨用户仅 static/IL/现有 unit 的有限声明。 |
| U6 | 对冻结 U0–U5 bytes/evidence 做独立最终审计，不增 source，不升 authority。 | receipt <code>u6-independent-final-audit-20260828T232640380Z</code>。 | passed；<code>P0/P1/P2=0/0/0</code>；source manifest 16,607 entries，SHA-256 <code>592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284</code>；frozen-root mismatch 0；process/pipe/LocalE2E temp residue 空。USER_MODE 100/100，替代此前失败 publication checkpoint。 |

U4 的一个先前 writer 已拒绝且只保留历史来源：其引用不存在的 Worker、在显式选择前启动、未等 C2 ACK 就接受选择、且越过 Desktop test ownership。该 isolated bytes 不构成当前 Worker/E2E 证据。

### 10.2 AI provider DAG

~~~mermaid
flowchart LR
  A0 --> A1
  A1 --> A2
  A1 --> A3
  A2 --> A4
  A3 --> A4
  A4 --> A5 --> A6
~~~

| 节点 | 目标与主要实现 | 关键提交/receipt | 测试、证据与结论 |
|---|---|---|---|
| A0 | 文档 rebase：定义两个通道、OpaqueEndpoint、zero-auto-network、无 fallback。 | ADR-006 及 7 份受控文档。 | docs-only；未重跑项目 gate；它是路线设计而不是实现 receipt。 |
| A1 | AI contracts/providers/tests/config schema；原子 config、revision lock、DPAPI/SecretRef、redaction、resolver/registry、Tom draft import。 | merged <code>698e770a35062cc4135872147a401dce40adcb51</code>；receipt <code>D:\wt\i2s-a1\.codex_tmp\a1-phase2-gate-092b7d6b3aeb4246928688323771e8b8</code>。 | AI <code>23/23 × 3</code>、opaque-endpoint schema vectors 9、receipt 167/167、Release 0/0、root 0、residue 0。A1 不含真实 Chat/Image HTTP。 |
| A2 | Chat channel adapter：单 route snapshot、protocol-specific bounded request/auth、request-time endpoint、no redirect/cookie、redacted error/health。 | source <code>55ee0993f71375ee0245cbee54815e7988fe04fd</code>；redirect fix <code>2678cb62be9ac9ff5a05c9a5b605a75c60effb5c</code>。 | Chat <code>23/23 × 3</code>；shared Release 0/0；只证明 Chat component boundary，非 live/paid/desktop/E2E。 |
| A3 | Image channel adapter：base64/URL、separate artifact client、no redirect/no auth forwarding、inspection/private cache。 | source <code>c7c4adcfcc80c732bfaf87b0dfea11294b4af741</code>；redirect fix <code>12b58ac69efe3175cf49a6ee129b3784b5b3da5c</code>。 | Image <code>20/20</code>；shared Release 0/0；只证明 image boundary，不写 Unity project。 |
| A4 | Desktop runtime/settings、Create/Preview/Settings wiring、entry-only secret/revoke、zero-auto-network、memory decoder。 | <code>fc986d11</code>、<code>ffc9f609</code>、<code>cc5ff806</code>。 | receipt 186/186；AI 77/77、Desktop 22/22、total 423/423、root 0、runtime/pipe/private-artifact residue 0；不是 A5 E2E。 |
| A5 | 真实 loopback <code>TcpListener</code> + production runtime/handlers 的 local E2E；Settings -> Chat -> Preview Image，含 URL/base64、restart/revoke/isolation/cleanup。 | source <code>9152c7e6</code>，remediation <code>14abb1d3</code>，merge <code>c6f9920f</code>；receipt <code>a5-final-acceptance-14abb1d3-bootstrap</code>。 | <code>P0/P1/P2=0/0/0</code>；11/11、209 SHA-256 hashes、root replay 0、tracked locks unchanged、A5 residue 0。无 handler injection、external traffic 或 paid call。 |
| A6 | 独立最终 AI provider audit；冻结 A0–A5，不能修复后再重跑并把结果充当独立审计。 | <code>.codex_tmp/a6-ai-provider-final-audit-20260829T000000Z/audit-summary.json</code>。 | <code>P0/P1/P2=0/0/0</code>；gate 434/434、schemas 23、source/root/residue 0、Broker smoke W24FS001/23、feed 39/39、18 ignored locks、tracked locks unchanged。ADR-006 100/100；非真实 paid auth/production release。 |

## 11. 开发过程中的重大转向与历史处理

### 11.1 从特权/服务路线转为普通用户本地路线

旧 ADR-004 及其 D1/D1R、ServiceHost、SCM/install、I1、R1、A1、B1 和相关 privileged E2E 曾探索 Windows service、SCM、LocalSystem、SACL、installer、issuer、loaded-image 等路线。它们不适合本项目已经确定的产品问题：当前用户主动使用、明确选择本地项目、只读的创作辅助工具。那套路线要求特权根、安装/注册、额外 live attestation 和不成比例的授权语义，却不能增加本产品所需的普通用户体验。

ADR-005 的决策因此不是“少做安全”，而是把安全边界改为与威胁模型匹配的：current-user isolation、不可重放 bootstrap、精确 child identity、受限 project locator、strict protocol、cleanup 和 redaction。跨用户、错误项目、旧会话与崩溃仍有闭环；同用户恶意程序与管理员则被明确移出 claim。

所谓“同用户对抗路线”也没有被悄悄降级为已交付能力：它被有意排除在产品目标之外。当前用户本身是 trusted principal，故一个同用户恶意进程可以拥有同等 OS 身份、调试/注入或读取本地状态；仅叠加 pipe 名、nonce、SACL、installer 或 issuer 不能把这类对手变成可可靠隔离的安全主体。若将来要对抗该对手，必须以独立用户/权限边界、部署模型和新的 ADR 重做 threat model，不能复用当前 <code>CurrentUserOnly</code> 结论。

因此：

- 旧 SCM/LocalSystem/SACL/installer/privileged issuer 链已**废弃为产品交付路线**；
- 禁止无新 ADR 和明确用户授权就静默重新引入 service、SCM mutation、privileged token、strict-SACL live requirement、loaded-image proof 或 enrollment；
- dormant 历史代码/receipt 可保留用于 provenance、回归比较和理解当时决策，但不得被继续实现/审计，也不得阻挡 U1–U6；
- 历史 “NO-GO” 仅描述当时的旧路线，不是当前 USER_MODE 的 NO-GO。

### 11.2 从 endpoint admission 到用户拥有的 opaque text

A0/早期 post-U6 plan 曾将 endpoint 更接近 URI admission 的设计讨论；最终 ADR-006 明确改为 <code>OpaqueEndpoint</code>。理由是配置保存的正确性与一次网络请求可否构造是不同问题：用户可能需要存 relay、friend、subscription 或非 URI 文本，保存层不应替其 trim/repair/重写，更不应在 save/start/navigate 时联网。

最终规则是 “bounded exact storage，request-time-only interpretation”。这也解释为什么计划文档中 “A1 active” 或非空 endpoint 的早期措辞不能覆盖最终 ADR-006 的“任意有界文本，含 empty/whitespace”结论。

### 11.3 以明确 ACK 和真实 bundle 替代看似可用的测试捷径

U4 的 rejected writer 与 U5 前期不完整尝试表明：只让 scripted peer 或 test hook 通过，不足以证明产品边界。最终方案固定为：

- 先 explicit selection，再启动 Worker；
- 先 U2 admission，再发送 U3 locator；
- 先严格 C2 ACK，再 <code>SelectAccepted</code>；
- U5 使用 staged 完整 Broker/Worker bundle 和 public <code>UserModeDesktopSession</code>；
- A5 使用真实 loopback listener 与 production <code>HttpClient</code> handlers，而非 handler injection。

这些转向使证据对应真实进程与 transport，而不是仅对应 test seam。

## 12. 工程控制：worktree、统一 gate、审计与一次 remediation 规则

### 12.1 Git/worktree 与所有权

当前工程已采用可提交的 Git 基线和隔离 worktree。每个有实现的工作包应在独立 branch/worktree 中进行，开工前冻结：

- 唯一目标、DAG 前置节点、精确 source/test/docs allow-list；
- expected base、baseline root manifest、package lock 集；
- 对应 writer 和 read-only auditor 的职责；
- 生成 receipt root 与交接材料。

历史上曾因 repository “unborn master” 而采用临时 local/无 commit 控制；那段例外仅是历史，不适用于当前已存在提交和 worktree 的仓库。它不授权当前任务跳过 Git、越过 allow-list 或修改已关闭节点。

### 12.2 统一 gate 与证据链

<code>eng/run-phase2-gate.ps1</code> 是统一 gate 主入口，结合：

- locked/no-restore Release build；
- <code>eng/verify-phase2-schemas.py</code> 的 23-schema 当前集合、positive/negative fixtures；
- target/full 测试、Desktop/AI/LocalE2E 专项测试；
- <code>eng/verify-offline-restore.ps1</code> 与 <code>eng/prepare-approved-feed.ps1</code> 的 approved local feed、lock/config 哈希；
- <code>eng/phase2-baseline-roots.json</code> 的 frozen root replay；
- default Broker smoke（empty stdout，stderr <code>W24FS001</code>，exit 23）；
- process、named pipe、LocalE2E temp、A5 private image temp 的 point-in-time residue snapshot。

runner 为每次执行创建 <code>.codex_tmp/phase2-gate/&lt;milestone&gt;-&lt;timestamp&gt;</code> receipt。receipt 是证据，不是产品源码；其 “residue 0” 表示采样时无残留，不应被表述为对历史永远不存在残留的证明。

### 12.3 独立审计与 remediation

控制方式是 writer 先按 scope 完成、冻结 bytes 和 receipts，再由独立 read-only audit 判断 P0/P1/P2。任何 finding 的 remediation 必须：

1. 明确属于**同一个尚未关闭**的工作包；
2. 只修复已发现问题，保持原 allow-list 或先获得新的显式里程碑授权；
3. 重跑规定 gate，重做 source/receipt/root/residue 对照；
4. 由独立审计验证后才关闭。

每个包只有**一次 remediation 开发回合**；不能用连续小修把 scope 演变成新功能，也不能在 final audit 中“顺手修复”再把自己修过的结果称为独立结论。包一旦 <code>STOPPED</code>，writer 与 auditor 都退休；后续包使用 fresh writer/fresh read-only auditor。此规则来自历史 program control，并作为当前新里程碑的推荐最小治理规则。

## 13. 验证、构建、schema、restore、lock 与 residue 的证据流程

建议把每次新里程碑的验证理解为一条顺序证据链：

~~~mermaid
flowchart LR
  S["Freeze scope / base / allow-list"] --> B["Locked restore & Release build"]
  B --> T["Target + full tests"]
  T --> C["Schema + protocol vectors"]
  C --> M["Default Broker smoke"]
  M --> R["Root replay / source manifest"]
  R --> L["Lock/feed verification"]
  L --> Z["Process/pipe/temp residue snapshot"]
  Z --> A["Receipt + independent read-only audit"]
~~~

具体要求：

| 检查 | 要证明什么 | 不能夸大的结论 |
|---|---|---|
| locked restore/package feed | 使用批准包和固定 <code>packages.lock.json</code>，防止依赖漂移。 | 不证明 provider 可用或安全授权。 |
| Release build（常用 <code>--no-restore</code> + <code>RestoreLockedMode=true</code>） | 目标程序集可从冻结依赖构建，warning/error 可量化。 | 不证明运行时 E2E。 |
| unit/target/full tests | codec、config、session、routing、UI、E2E 的行为样本。 | 不等于所有攻击面均已抵抗。 |
| schema verifier | 当前 23 schema 的版本、正反例、opaque endpoint vectors。 | 不验证 endpoint 可联网。 |
| default Broker smoke | 默认入口没有意外 listener/worker/特权行为。 | 不证明 <code>--user-mode-...</code> child route。 |
| frozen-root/source manifest | 所审的 bytes 与 scope 相符，root replay 无 drift。 | 不把 hash 升格为 authority。 |
| residue snapshot | 在验收时无 Broker/Worker process、VFX Composer pipe、owned temp/cache residue。 | 只是一时点快照。 |
| assembly SHA-256/MVID binding | E2E stage 的 Broker/Worker 与被测 product assembly 对应。 | 不证明真实商用 provider/外网。 |
| receipt + 独立 audit | 用可复查数据关掉 P0/P1/P2。 | 不替代新需求的审查或生产发布。 |

## 14. 最终验收数字与 receipt 索引

| 里程碑 | 主 receipt/提交 | 最终关键数字 |
|---|---|---|
| U3 | <code>0123616e…</code> / <code>027ba074…</code> | Broker 8/8；Unity 9/9；unified Broker 179/179。 |
| U4 | <code>2295b022…</code> / <code>e1a6a9a…</code> | Protocol 108/108；Client 14/14；Broker 183/183；Desktop 12/12；schema 22/13/14/236；Release 0/0。 |
| U5 | <code>U5-local-e2e-independent-acceptance-20260828T144950082Z</code> | P0/P1/P2=0/0/0；17 owned files；Protocol 108/108、Client 16/16、Broker 183/183、LocalE2E 17/17；receipt 121 rows；root/residue 0。 |
| U6 | <code>u6-independent-final-audit-20260828T232640380Z</code> | P0/P1/P2=0/0/0；source manifest 16,607；SHA-256 <code>592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284</code>；root mismatch 0；residue empty。 |
| A1 | <code>698e770a…</code>；<code>D:\wt\i2s-a1\.codex_tmp\a1-phase2-gate-092b7d6b3aeb4246928688323771e8b8</code> | AI 23/23 ×3；opaque vectors 9；receipt 167/167；Release 0/0；root/residue 0。 |
| A2 | <code>55ee0993…</code> + <code>2678cb62…</code> | Chat 23/23 ×3；shared Release 0/0。 |
| A3 | <code>c7c4adcf…</code> + <code>12b58ac6…</code> | Image 20/20；shared Release 0/0。 |
| A4 | <code>fc986d11</code> / <code>ffc9f609</code> / <code>cc5ff806</code> | P0/P1/P2=0/0/0；receipt 186/186；AI 77/77；Desktop 22/22；total 423/423；root/residue 0。 |
| A5 | <code>a5-final-acceptance-14abb1d3-bootstrap</code>；<code>9152c7e6</code>/<code>14abb1d3</code>/<code>c6f9920f</code> | P0/P1/P2=0/0/0；11/11；209 SHA-256 hashes；root 0；tracked locks unchanged；A5 residue 0。 |
| A6 | <code>.codex_tmp/a6-ai-provider-final-audit-20260829T000000Z/audit-summary.json</code> | P0/P1/P2=0/0/0；gate 434/434；schemas 23；source/root/residue 0；feed 39/39；18 ignored locks；tracked locks unchanged。 |

完整 receipt 路由、owned paths 和由哪些数字能证明/不能证明什么，以 <code>docs/coordination/W24_EVIDENCE_INDEX.md</code> 为准。

## 15. 仓库导航

| 想了解什么 | 首选文件/目录 |
|---|---|
| 当前总体状态与 receipt | <code>docs/coordination/W24_EVIDENCE_INDEX.md</code> |
| 当前控制/所有权/历史分界 | <code>docs/coordination/W24_PROGRAM_CONTROL.md</code>；<code>docs/coordination/W24_WORK_PACKAGE_REGISTRY.md</code> |
| 普通用户安全设计 | <code>docs/rules/ADR-005_USER_MODE_BROKER_WORKER_ARCHITECTURE.md</code> |
| AI 路由/endpoint/secret 规范 | <code>docs/rules/ADR-006_AI_PROVIDER_TWO_CHANNEL_ROUTING.md</code> |
| 阶段计划及实际 closeout | <code>docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md</code>；<code>docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md</code> |
| protocol IPC | <code>src/VFXComposer.Protocol/Ipc/UserModeDesktopSessionCodec.cs</code>；<code>src/VFXComposer.Protocol/Registration/WorkerProjectLocator.cs</code> |
| Desktop/Client session | <code>src/VFXComposer.Client/UserModeBrokerProcessHost.cs</code>；<code>src/VFXComposer.Client/UserModeDesktopSession.cs</code>；<code>apps/VFXComposer.Desktop/ViewModels/MainWindowViewModel.cs</code> |
| Broker project/session admission | <code>services/VFXComposer.Broker/Ipc/UserModeDesktopBrokerHost.cs</code>；<code>services/VFXComposer.Broker/Ipc/UserModeNamedPipeServer.cs</code>；<code>services/VFXComposer.Broker/Registration/UserModeProjectSelectionStore.cs</code> |
| Worker restricted read | <code>services/VFXComposer.UnityWorker/UserModeUnityWorkerHost.cs</code>；<code>services/VFXComposer.UnityWorker/UserModeWorkerBootstrapPeerCodec.cs</code> |
| AI contracts/config/secret | <code>src/VFXComposer.AI.Contracts</code>；<code>src/VFXComposer.AI.Providers/ProviderConfigurationStore.cs</code>；<code>ProviderConfigurationRevisionLock.cs</code>；<code>ProviderSecretStore.cs</code>；<code>ProviderConfigurationResolver.cs</code> |
| Chat/Image adapters | <code>src/VFXComposer.AI.Providers/Chat/ChatChannelGateway.cs</code>；<code>src/VFXComposer.AI.Providers/Image/OpenAiCompatibleImageGateway.cs</code>；<code>PrivateImageArtifactCache.cs</code> |
| Desktop AI settings/preview | <code>src/VFXComposer.AI.Providers/Desktop</code>；<code>apps/VFXComposer.Desktop/ViewModels/SettingsViewModel.cs</code>；<code>apps/VFXComposer.Desktop/Services/PrivateImagePreviewDecoder.cs</code> |
| Gate/restore/schema | <code>eng/run-phase2-gate.ps1</code>；<code>eng/verify-phase2-schemas.py</code>；<code>eng/verify-offline-restore.ps1</code>；<code>eng/approved-packages.json</code>；<code>eng/phase2-baseline-roots.json</code> |
| 历史特权路线 | ADR-004、历史段和 dormant ServiceHost 相关目录；仅作 provenance，非运行路线。 |

## 16. 已知限制与严禁作出的声明

1. U6 关闭的是普通用户本地只读创作路线；不是 Windows service/SCM/authority/command/mutation/evidence/verdict 产品发布。
2. U5 的 cross-user 证据是 <code>CurrentUserOnly</code> static/IL 与已有 unit 的有限结论；未创建第二帐户进行 literal multi-user E2E。
3. USER_MODE 的 hash/signature、source manifest、MVID/receipt 只用于完整性和可复查性，不能证明授权或抵抗 offline/admin/same-user attacker。
4. A6 关闭的是 AI routing/configuration/loopback 验收范围；不是已经拥有真实 provider credential、真实付费/生产端点、真实生产 SLA 或发行批准。
5. config 接受 OpaqueEndpoint 不能证明 endpoint 可以被 .NET 解释、DNS 可达、TLS/认证成功、上游兼容或模型可用。
6. Image artifact 是私有、非受信任 temporary output；当前没有自动 Unity export/project write。
7. “residue 0”“root replay 0”“build 0 warnings/errors”均是相应 receipt 的确定范围/时点结论，不能扩大成全时段或全攻击面的绝对保证。
8. 历史旧计划中曾出现的 active/NO-GO/未开始/特权依赖不再是当前状态；引用这些文字时必须带 historical/superseded 标签。

## 17. 新里程碑启动规则与建议计划

已关闭的 U0–U6 和 A0–A6 不应被重新打开以容纳新需求。任何新需求（例如显式 image export、更多 provider protocol、project mutation、真实 provider acceptance、多用户安全目标或重新引入服务）必须先建立新的 milestone，最少包含：

1. **新的 ADR 与非目标**：说明业务价值、是否改变威胁模型，明确哪些旧边界仍然不变；若触及 service/SCM/privilege/authority，必须有用户的显式授权。
2. **独立 DAG**：定义最小 nodes、先后依赖、冻结 completion algorithm；不能把历史节点的 GO 解释为新功能已验收。
3. **精确 allow-list 和 worktree**：以 clean expected base 开工，限制源码/测试/schema/gate/docs 所有权，并明确生成 receipt 的位置。
4. **安全设计先行**：对 project path、secret、network、artifact/export、lifecycle、rollback 做 threat model；写明 fail-closed 行为和不作声明。
5. **实施与验证**：先 target tests，再 unified gate、locked restore、schema、default smoke、root replay、lock/feed、residue、assembly binding。
6. **一次 remediation + 独立 audit**：只允许原包一次范围内修复；final audit 保持 read-only，关闭后 writer/auditor 退休。
7. **新的 final receipt**：用当前计划定义的证据关闭，不覆盖旧 receipt；若结果不满足，保持该新里程碑 NO-GO，而不倒改本文的当前闭环。

一个安全的建议顺序是：先做 docs-only 需求/威胁模型节点，再做纯 contract/schema，再做受限 local test，最后才考虑显式 UI 和 E2E。对于“把 AI 图像放入 Unity 项目”这样的需求，必须作为单独 export/mutation milestone：用户显式确认目标、路径 containment、格式/覆盖策略、原子写入/回滚、审计和 Unity-side 验证都要重新设计；不得从当前 private preview/cache 路径隐式升级。

---

### 结论

VFX Composer 当前已经完成的是一个收敛且可审计的本地创作基础：普通用户显式选择项目后的 Broker/Worker 受限只读路径，以及 Desktop 中两个独立、显式、零 fallback 的 AI 通道。其安全性来自边界清晰、严格关联、最小路径能力、无自动网络与可复查的冻结证据，而不是特权服务或把历史实验性路线误当成现状。后续扩展应继承这些边界，并以新的 ADR、DAG、worktree、gate 和独立 audit 开启。
