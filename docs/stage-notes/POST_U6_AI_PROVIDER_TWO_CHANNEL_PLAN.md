# POST-U6 AI Provider 双通道实施计划

> **状态：FUTURE / 未启动 / 不构成当前 Phase-2 的启动授权。** 本计划只能在 ADR-005 的 U6 已完成独立审计并得到 scoped GO 后，再由新的控制包显式发布。它不修改 U0–U6 的 DAG、冻结估算、Broker/Worker/Unity 边界或当前 NO-GO 结论。
>
> **计划输入：** 用户要求的两个独立 API 通道，以及固定版本 [`snowpeak008/Tom_doc@dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`](https://github.com/snowpeak008/Tom_doc/tree/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf) 的 Provider 设计审阅。本文件是计划，不实现 AI、网络、设置 UI 或迁移代码。

## 1. 位置、范围与不可变边界

ADR-005 当前路线仍然是：

`U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`。

本计划是该路线验收后的一个**独立后续产品链**，不能反向成为 U4、U5 或 U6 的依赖、验收证据或生产 GO 理由。新链的精确依赖为：

`A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`

其中 `A2`（Chat / LLM）和 `A3`（Image Generation）只有在 A0 已冻结公共契约且文件所有权完全不重叠时才可并行。每一节点都必须在前序 gate 通过、源字节冻结、独立审阅完成后才可启动下一节点。

本链只为当前 Windows 用户的 Desktop 产品配置两个 AI 通道：

| 通道 | 唯一用途 | 运行时选择 | 绝对规则 |
|---|---|---|---|
| `chat-llm` | 所有对话、提示词辅助、LLM 文本生成与结构化 LLM 请求 | 一个显式 `ChatLlm` channel binding | 任何对话/LLM 调用都必须经过 `IAiGateway`；功能代码不得持有 endpoint、key、adapter 或 `HttpClient`。 |
| `image-generation` | 所有生图请求与其结果下载/缓存 | 一个显式 `ImageGeneration` channel binding | 任何生图调用都必须经过同一个 `IAiGateway` 的 image lane；不得借用 chat lane 或直接调用图片 URL。 |

“官方、API 中转、朋友提供、订阅、定制”是用户看到的 **Origin**，不是协议，也不是安全级别：

`official | relay | friend | subscription | custom` 只影响展示、导入提示、用户确认和诊断文案。它们**不能**自动选择请求格式、认证方式、模型、endpoint、重试目标或另一条通道。一个订阅来源也不会因此获得读取浏览器 Cookie、启动本机 CLI、扫描 PATH 或代表用户登录第三方网站的权限；第一版只使用用户明确录入且受本地密钥存储保护的凭据。

以下边界在 A0–A6 全程保持为真：

- 不进入 `services/VFXComposer.Broker/**`、任何 Worker、`project/**`、`project/Packages/**` 或 Unity Editor/runtime 代码；不增加 Broker/Worker/Unity 协议消息、网络路由或项目读写权限。
- Desktop 仍然没有直接 Unity 项目 I/O。AI 结果只是未受信任的文本建议、结构化候选或缓存图片，不能自动写入 Unity 项目、Recipe、Patch、资产、场景、设置或任意用户路径。
- 任何 LLM 输出仍须经过既有 Schema、Validator、受限 Patch、人工明确动作和相应后续 gate；S2 的研究结论（稳定语义路径、未知字段不静默忽略、AI 不能自行应用修改）不因接入 Provider 而放宽。
- 不引入 Windows Service、SCM、特权令牌、公开 HTTP/TCP listener、远程 MCP、旁路 Desktop-to-Worker 路由或“AI 调用即 authority”的语义。
- A5 的端到端测试只允许本机 mock；它不是对真实官方、中转、朋友或订阅服务的可用性证明，CI 中不得放入真实 URL、账户、订阅、API key 或图像内容。

## 2. 目标架构：一条强制 Gateway、两个显式绑定

所有业务功能只依赖公共抽象，网关在单一位置完成通道解析、配置验证、认证、协议 adapter 选择、请求发出、失败归类与脱敏日志：

```text
Desktop feature / ViewModel
          |
          v
      IAiGateway.Execute(channel, request)
          |
          v
  immutable settings snapshot
          |
          +--> ChannelBinding (exactly one profile + capability)
                    |
                    v
   ProviderProfile -- ProtocolBinding -- Endpoint -- Auth/SecretRef
                    |                       |
                    +---- Capability set -----+
                                                v
                           explicit Chat adapter OR explicit Image adapter
                                                |
                                                v
                              request-scoped HTTP client message / safe cache
```

`IAiGateway` 是产品内进程组件，不是假设存在的外部“中转网关”。它必须是所有 outbound AI 调用的唯一入口。功能调用不得传入裸 `profileId`、任意 URL、header、token、adapter 类型或备用 Provider；只允许传 `AiChannel` 与已版本化、无密钥的请求 DTO。设置界面可以使用单独的受限管理 API 测试某个候选 capability，但不能把该测试 API 当作功能调用旁路。

### 2.1 六个概念必须分离

| 概念 | 负责的事实 | 明确不负责的事实 |
|---|---|---|
| `ProviderProfile` | 稳定 ID、显示名、Origin、启用状态、配置 revision，以及对其他对象的引用。 | 不能内嵌明文或 DPAPI 密文、不能根据名称推断协议、不能自动成为某通道的默认值。 |
| `ProtocolBinding` | 用户确认的版本化 `ProtocolId` 和该协议允许的 request/response shape。示例可以包括 `openai.chat-completions.v1`、`openai.responses.v1`、`anthropic.messages.v1`、`gemini.generate-content.v1`、`openai.images.v1`，以及经代码实现和测试的将来 image protocol。 | 不是 Origin；不能由 Base URL、模型名、网站名或导入 JSON 在运行时猜测。 |
| `Endpoint` | 已规范化的绝对服务根、协议允许的 path shape、超时和 TLS/本地开发例外。请求 path 必须由显式协议 adapter 按已验证 shape 构造。 | 不能包含 user-info、API key、任意 header、查询串凭据或用字符串特征改写为另一协议。 |
| `Auth` | 认证 kind（例如 API key / bearer token）与不透明 `SecretRef`。各协议决定固定 header/name/location，不能由用户输入任意 header 名。 | 不能向 UI、日志、DTO、缓存键或异常暴露 secret，也不能被 Origin 取代。 |
| `Capability` | 一个 profile 对每个已支持操作的显式能力和模型配置，例如 `chat.generate-text` 或 `image.generate`。 | 不能以“同一个模型名看起来支持图片”为由越过 adapter、protocol 或 channel 校验。 |
| `ChannelBinding` | `ChatLlm` 或 `ImageGeneration` 到一个 `profileId + capabilityId` 的一对一用户选择。 | 不能含 fallback 列表、优先级列表、隐式默认 profile 或跨通道复用规则。 |

同一 ProviderProfile 可以在技术上声明两项能力（例如同一官方账号同时有文字与图片模型），但两个 channel binding 仍须由用户分别选择、分别校验、分别显示状态。未绑定或绑定失效时，对应功能返回稳定的“该通道未配置/不支持/未验证”错误，**零网络调用**。

### 2.2 Fail closed：禁止一切隐式 fallback

下列行为全部禁止，并应有负面测试：

- 未知或旧 `ProtocolId` 不能退回 OpenAI-compatible、Chat Completions 或任意默认 adapter。
- `Origin=relay`、模型名、URL、网站名称或导入内容不能在运行时自动识别协议；导入器最多生成未激活的建议，用户必须看到、确认并通过 capability 测试。
- `chat-llm` 无配置时不能尝试 image profile；`image-generation` 无配置时不能尝试 chat profile；一个 channel 的失败不能切换另一个 profile。
- 超时、401、429、5xx、解析失败、下载失败、缓存失败或取消不能自动重试到不同 endpoint、不同模型、不同认证或不同 Origin。若协议允许有限重试，只能对**同一已绑定 profile、同一 endpoint、同一请求**执行，且要有取消、幂等性和可观测的 attempt 上限。
- 不能通过 `Get(unknown)`、空字符串、环境变量、系统 PATH、浏览器状态、最后成功配置或“当前唯一已启用 Provider”选择调用目标。

### 2.3 请求、网络与日志的统一规则

- Gateway 从一次不可变配置快照解析完整链，先校验 channel binding、profile、capability、protocol、endpoint、auth 和策略，再建立请求。任一环节失败即终止；禁止先发探测请求再决定协议。
- 采用长寿命/工厂管理的连接池，但认证 header 只放在每个 `HttpRequestMessage`；禁用 cookie 持久化。禁止把 `Authorization`、token、endpoint query、Prompt、原始响应、图片返回 URL 或二进制数据写入共享 client、日志、异常或 telemetry。
- endpoint 默认要求 HTTPS。仅在用户明确启用的本机开发例外中允许 loopback HTTP，且不得附带生产 secret；任何非 loopback 明文 HTTP 一律拒绝。
- 每个调用带取消令牌、有限 timeout、stable correlation ID 和脱敏诊断码。用户可看到 channel、profile 显示名、Origin、ProtocolId、HTTP 状态族和耗时；不得看到 key、secret ref 内容、完整 prompt、完整 endpoint、返回体、签名图片 URL、project path 或原始图片字节。
- 原始 Provider request/response 默认永不落盘。若未来需要可支持的诊断导出，必须是独立 ADR/显式用户导出、逐字段 redaction、默认关闭且不在 A0–A6 范围内；不能照搬“保存 raw response”的默认能力。

## 3. 用户配置与本地安全存储

AI 设置属于当前 Windows 用户的应用数据，而非 Unity 项目配置。建议根为：

```text
%LOCALAPPDATA%\VFXComposer\ai\
  ai-provider-settings.v1.json
  ai-provider-settings.v1.json.bak
  image-cache\
```

`ai-provider-settings.v1.json` 是一个版本化、原子提交的聚合文档：公开 profile/binding 元数据与一个逻辑隔离的 `secrets` 表处于同一次提交中。Profile 仅存 `SecretRef`；只有受限的 `ISecretStore` 能解析 `SecretRef` 到对应的 DPAPI envelope。这样既保持 profile/auth 分离，也避免两个独立文件在崩溃时产生无法判断的交叉事务。

### 3.1 保护、原子性与恢复

- Secret plaintext 仅在保存或请求组装的最短必要时间存在。使用 Windows `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`，并使用本产品独有、版本化的 entropy/purpose；不能复用 Tom 的 entropy、不能跨用户解密。
- JSON 中保存的是 `secretRef -> { protector: "dpapi-current-user", version, protectedPayload }`，不是明文。所有公共请求/响应/配置 DTO 都只能携带 `SecretRef`，绝不能有 `ApiKey`、`Token`、`Authorization` 字段。
- 原子 JSON 写入须在目标同卷目录中先 `CreateNew` 临时文件、UTF-8 序列化并检查大小、`WriteThrough` + `Flush(true)`，随后使用 replace/move 提交；保留上一份 `.bak`，成功后不得遗留 `.tmp`。读入时须做最大字节数、严格 schema、重复键、未知字段和版本校验。
- 损坏、无法解密、权限不足或版本未知时 fail closed：保留主文件与 `.bak`，显示可恢复诊断并禁止 AI 调用；不得静默用空默认配置覆盖用户数据。密钥删除也须是用户明确动作，不能由空输入或失败导入隐式删除。
- 配置修改会生成新的 revision/fingerprint，并使先前验证状态失效；验证成功只说明该 profile/capability 在该 revision 下可达，绝不启用或替换另一个 channel binding。

### 3.2 Tom_doc 配置导入：复用结构思想，不迁移密文

导入是一次本地、用户确认的**草稿导入**，不是自动迁移或自动激活。导入器应为固定版本的 Tom JSON 维护严格 allow-list，最大化复用用户已经填写的非敏感设置，同时把安全边界转换为本产品自己的实现：

| Tom 字段类别 | 本计划的处理 |
|---|---|
| `Id`、`Type`、`DisplayName`、`Enabled`、`BaseUrl`、`DefaultModel`、`RelayWebsiteName`、`RelayProtocol`、`TimeoutSeconds`、`UseJsonSchema`、`SaveRawResponse` | 只作为未受信任的草稿元数据。经过格式、长度、endpoint 和枚举校验后映射到新 profile 的显示名、Origin 建议、endpoint、模型和 capability 草稿；不保留 Tom 的单一 enabled 语义，也不自动创建 channel binding。 |
| `ApiKeyProtected` | **绝不复制、绝不解析、绝不解密、绝不写入**新配置。Tom 密文不会迁移；导入完成后 UI 必须明确提示用户重新输入 credential，由本产品用自己的 DPAPI CurrentUser purpose 新建 `SecretRef`。 |
| `VerificationAvailable`、`VerificationSignature`、`VerificationMessage`、`LastVerifiedAtUtc` | 不信任、不继承为可用状态。可在导入报告中说明“旧验证状态已丢弃”，新 profile 必须在本产品的选定 capability 下重新测试。 |
| `CommandPath`、订阅 sidecar/CLI 线索 | 拒绝为可执行配置；不从 Tom 或本机启动外部命令，不读取 PATH/Cookie。若用户选择 `subscription` Origin，仍要明确选择本产品支持的协议和用户录入的 SecretRef。 |

Tom `RelayProtocol=auto` 或其检测摘要只能显示为建议。它不能创建 active binding，不能在调用时再猜测，不能把 `relay` 转换成某个 adapter。导入预览必须把“建议的协议”与“用户确认的 ProtocolId”并列显示；未确认即不能保存为可调用 profile。

## 4. 图片通道的 adapter、下载与缓存边界

图片不是 Chat 响应的一个特殊字符串。`IImageGenerationAdapter`、图像响应 DTO、下载器、解码校验和缓存必须与 `IChatLlmAdapter` 分开实现，并同样由 Gateway 的 `ImageGeneration` lane 选择。

1. Image adapter 仅接受版本化 `ImageGenerationRequest`（prompt、尺寸、数量、模型/quality 的受限枚举、取消令牌），并只为已绑定且显式声明 `image.generate` capability 的 profile 组装协议指定请求。第一版本不接受任意 source path、任意 URL、任意 headers，也不把本地 Unity 资产自动上传；image-to-image、编辑、mask、视频等能力须以后续 capability/adapter/gate 单独加入。
2. Provider 响应中的 base64 图像或下载 URL 是不可信输入。URL 下载仅经受限 downloader：仅允许 HTTPS（或测试/显式开发 loopback）、固定大小/重定向/超时限制、禁止将 API 认证转发到资源 host、校验最终 host/scheme、Content-Type、文件签名、像素尺寸和解码上限。任何失败都只使本次 image 请求失败，不能转去 chat 或另一个 profile。
3. 解码后的图片按 SHA-256 内容地址写入 `%LOCALAPPDATA%\VFXComposer\ai\image-cache\`；manifest 使用同一原子 JSON 原语。缓存文件名和公开日志不含 prompt、model、endpoint、用户路径或 secret。缓存配额、LRU 清理、锁/并发写入和损坏 entry 都须 fail closed/可恢复，且清理只能作用于已验证的 image-cache 根。
4. Desktop 只从已验证的本地缓存展示 `GeneratedImageArtifact`。Provider URL 不作为长期显示或共享链接。用户若要导出或导入图片，必须使用明确的 Desktop 用户动作与目标选择；A0–A6 不提供自动写入 Unity 项目、Assets、Recipe、Patch 或项目外任意目录的路径。
5. 图片缓存、下载错误和缩略图 telemetry 一律不保存二进制内容、prompt 或原始服务响应。测试可使用合成像素/fixture；CI 不得下载真实图像。

## 5. 建议的项目边界与依赖方向

以下是 A0 的建议，而不是当前创建项目的授权。它保持现有 `VFXComposer.Protocol` 的 IPC/Unity 职责不被 AI SDK 污染：

| 未来项目/根 | 职责 | 允许依赖 | 禁止依赖 |
|---|---|---|---|
| `src/VFXComposer.AI.Contracts/` | 纯 BCL 的 channel/profile/capability/请求/响应/诊断契约与 `IAiGateway`、设置管理抽象。 | BCL。 | Avalonia、`System.Net.Http` 实现、Desktop、Broker、Worker、Unity、项目文件系统。 |
| `src/VFXComposer.AI.Contracts.Tests/` | contract schema、golden vector、unknown-field、依赖边界和无 secret DTO 测试。 | Contracts、测试框架。 | Provider HTTP、真实网络、Desktop/Broker/Unity。 |
| `src/VFXComposer.AI.Providers/` | profile store、DPAPI secret store、atomic JSON、explicit adapter registry、Gateway、HTTP policy、image downloader/cache。 | Contracts、BCL/经批准的最小依赖。 | Broker/Worker/Unity、任意项目写入、UI 控件。 |
| `src/VFXComposer.AI.Providers.Tests/` | fake handler/server、存储/导入/adapter/缓存/redaction/negative tests。 | Contracts、Providers、测试框架。 | 真实密钥、真实 Provider、互联网。 |
| `apps/VFXComposer.Desktop/` 与 `apps/VFXComposer.Desktop.Tests/` | 设置页、channel 选择、状态显示和仅依赖 Gateway/管理抽象的 feature wiring。 | Contracts + Providers 的公开接口。 | `HttpClient` 直连、secret plaintext、Broker/Worker/Unity 项目 I/O。 |

`VFXComposer.sln`、central package/lock 及上述新项目的精确文件清单必须在 A0 发布时冻结。若 A0 审阅发现现有 `Desktop.Tests` 不能稳定执行 mock UI/E2E，可新增一个仅含 mock 的 `apps/VFXComposer.Desktop.Ai.E2E.Tests/`；不能为方便测试把测试服务器或网络能力塞进 Broker、Worker 或 Unity。

## 6. 里程碑执行卡

### A0 — 双通道契约与所有权冻结

**目标**

在 U6 scoped GO 后，创建纯契约层并冻结双通道语义、诊断词汇、依赖方向、配置 schema 和精确所有权。A0 的重点是让“所有调用经过 Gateway”成为可编译的产品边界，而不是先接任何供应商。

**计划拥有根**

- `src/VFXComposer.AI.Contracts/**`
- `src/VFXComposer.AI.Contracts.Tests/**`
- `VFXComposer.sln`，以及仅当新项目确实需要时的受控 package/lock 变更
- A0 发布时明确列出的 `docs/` 契约 schema/hand-off 文件

**API**

- `AiChannel` 只能为 `ChatLlm`、`ImageGeneration`；`AiOriginKind` 固定为 `Official`、`Relay`、`Friend`、`Subscription`、`Custom`。
- `ProviderProfile`、`ProtocolBinding`、`EndpointDefinition`、`AuthDescriptor(SecretRef)`、`CapabilityDefinition`、`ChannelBinding`、不可变 `AiSettingsSnapshot`；任何 DTO 都不得含 secret plaintext/ciphertext。
- `IAiGateway.ExecuteChatAsync(ChatRequest, CancellationToken)` 与 `IAiGateway.GenerateImageAsync(ImageGenerationRequest, CancellationToken)`；业务 API 没有 profile override。
- `IAiProviderSettingsAdmin` 仅用于列出/编辑/测试草稿和提交 binding；`TestProfileCapabilityAsync(profileId, capabilityId, ...)` 与运行时 API 语义隔离。

**测试**

- JSON golden vectors：两通道、未知 enum、未知/重复字段、缺少 binding、cross-channel capability 和 stale revision 都要拒绝。
- 编译/静态依赖测试证明 Contracts 不引用 Desktop、Avalonia、HTTP 实现、Broker、Worker、Unity 或项目路径 API；请求 DTO 不能序列化出 `apiKey`、`token`、`authorization` 等字段。
- Gateway public API 形状测试证明 feature request 无 endpoint/profile/adapter/fallback 参数，且不可能构造“第二选择”。

**门禁**

- U6 审计已 frozen/accepted；A0 的所有权清单与当前 U0–U6 控制文档无冲突。
- 两条 channel 均有一个且只有一个显式 binding 模型；Origin 与 Protocol 在 schema 上不可互换。
- Contracts/Contracts.Tests Release build 与全量静态边界测试为零 warning/zero failure。

**STOP**

不写 DPAPI、JSON store、HTTP、adapter、图片缓存或 Desktop UI；不接真实服务；不改 Broker/Worker/Unity；不把任何 AI 输出接入写入路径。

### A1 — 安全设置、SecretRef、导入与 Gateway 配置解析

**目标**

实现 per-current-user 的设置聚合、DPAPI CurrentUser secret table、原子 JSON、严格配置验证、Tom 草稿导入以及 Gateway 的 deterministic binding resolver。完成后仍没有功能网络调用。

**计划拥有根**

- `src/VFXComposer.AI.Providers/Configuration/**`
- `src/VFXComposer.AI.Providers/Security/**`
- `src/VFXComposer.AI.Providers/Storage/**`
- `src/VFXComposer.AI.Providers/Import/**`
- `src/VFXComposer.AI.Providers/Gateway/ConfigurationResolver.cs`
- 对应 `src/VFXComposer.AI.Providers.Tests/{Configuration,Security,Storage,Import,Gateway}/**`

**API**

- `IAiSettingsStore`、`ISecretStore`、`IAtomicJsonStore`、`ITomProviderDraftImporter`、`IChannelBindingResolver`；profile 读取返回无 secret snapshot。
- `ProtectCurrentUser(secretRef, plaintext)` / `ResolveSecret(secretRef)` 只在 Providers 内部可见；公共层仅见 `SecretRef`。
- import result 包含映射字段、被丢弃安全字段、需要用户重新录入 credential 的状态和未验证原因，绝不携带被丢弃密文原文。

**测试**

- DPAPI 当前用户 round-trip、另一个/无效 DPAPI payload fail closed、JSON 中无明文、SecretRef 不可由另一个 profile 越权解析、修改后 verification revision 失效。
- 原子写入崩溃/replace/move/`.bak`/临时文件/最大大小/损坏 JSON/重复键/未知字段测试；不能静默清空覆盖配置。
- Tom fixture 导入测试覆盖普通 official/relay/friend/subscription/custom 草稿：安全 allow-list、`ApiKeyProtected` 不复制、不解密、不输出，verification 不继承，`CommandPath` 拒绝，`auto` 仅建议且不能绑定。
- Resolver 的 missing/disabled/unsupported/unknown-protocol/wrong-channel/no-secret/invalid-endpoint cases 都断言**尚未触发任何 HTTP handler**。

**门禁**

- 所有持久化 secret 都受本产品 DPAPI CurrentUser purpose 保护，profile JSON 只保留 `SecretRef`。
- 同卷 atomic commit、备份恢复和 redaction 测试完整通过；A1 不允许出现基于名称、URL、模型或 `Enabled` 的自动选择。
- 依赖扫描证明 Providers 仍不引用 Broker、Worker、Unity、`project/**` 或 UI 控件。

**STOP**

不注册协议 adapter、不发送 HTTP、不生成/下载/缓存图片、不增加 Desktop UI。订阅来源只是一项元数据/credential 草稿，不能启动 CLI、读取浏览器或联网登录。

### A2 — Chat / LLM adapter lane（可与 A3 并行）

**目标**

在已冻结 A0 contracts 与 A1 resolver 上，为已明确选择的 chat ProtocolId 实现版本化 text/structured-output adapters，并让所有 Chat / LLM feature 都只能经 `IAiGateway.ExecuteChatAsync`。

**计划拥有根**

- `src/VFXComposer.AI.Providers/Adapters/Chat/**`
- `src/VFXComposer.AI.Providers/Gateway/ChatGateway.cs`
- `src/VFXComposer.AI.Providers/Http/Chat/**`
- `src/VFXComposer.AI.Providers.Tests/Adapters/Chat/**`
- `src/VFXComposer.AI.Providers.Tests/Gateway/Chat/**`

这些根与 A3 的 `Adapters/Image`、`Gateway/Image`、`Http/Image`、`Tests/.../Image` 必须保持不相交；公共协议枚举不得在并行阶段修改。

**API**

- `IChatLlmAdapter` 只接受已解析的 `ResolvedChatProfile` 和版本化 `ChatRequest`，返回无 raw transport 的 `ChatResult`。
- explicit adapter registry 以 `ProtocolId + AiChannel.ChatLlm` 精确查找；未注册即返回 stable unsupported diagnostic，不得返回默认 adapter。
- HTTP policy 使用请求级认证、固定 protocol header/path/response parser、取消与上限 timeout；结构化输出仅在 capability 明示时启用。

**测试**

- 以 fake `HttpMessageHandler` 对每个支持的 Chat ProtocolId 验证 method/path/payload/header/result parser；不允许真实网络。
- 401、429、5xx、malformed JSON、取消、timeout、未启用 profile、缺 secret、wrong capability、unknown protocol 与 adapter throw 全部只失败当前请求。
- 负面测试证明 unknown protocol 不回落 OpenAI-compatible，relay/friend/subscription/custom Origin 不能改协议，失败不访问第二个 profile，Desktop feature code 不可直接 new/send `HttpClient`。
- 日志/异常 snapshot 断言不含 API key、Bearer 值、prompt、完整 URL、response body 或 SecretRef payload。

**门禁**

- Chat feature 到 Gateway 的静态调用图只有一条出口；fake handler 的调用计数在任何 resolve failure 时为零。
- 所有已支持 ProtocolId 都有请求/响应 golden fixture 与 fail-closed unknown case；同 profile 的有限 retry 若实现，必须有明确 attempt/取消测试。
- A2 变更不触及 A3 roots、Broker、Worker、Unity 或项目写入代码。

**STOP**

不实现 image request/adapter/download/cache；不添加 per-feature provider 下拉框或 profile override；不自动应用 LLM 输出、更不写入项目。

### A3 — Image Generation adapter、受限下载与缓存 lane（可与 A2 并行）

**目标**

在同一 Gateway/配置规则下，为 `ImageGeneration` 建立独立 adapter、受限 response downloader、image validation 和 content-addressed cache；它不能复用 Chat adapter 的解析/备用逻辑。

**计划拥有根**

- `src/VFXComposer.AI.Providers/Adapters/Image/**`
- `src/VFXComposer.AI.Providers/Gateway/ImageGateway.cs`
- `src/VFXComposer.AI.Providers/Http/Image/**`
- `src/VFXComposer.AI.Providers/Images/**`
- `src/VFXComposer.AI.Providers.Tests/Adapters/Image/**`
- `src/VFXComposer.AI.Providers.Tests/Gateway/Image/**`
- `src/VFXComposer.AI.Providers.Tests/Images/**`

**API**

- `IImageGenerationAdapter`、`IImageResponseDownloader`、`IImageCache`、`GeneratedImageArtifact`；artifact 只暴露已验证缓存 ID/受限本地读取能力，不暴露 provider URL 或 Authorization。
- adapter registry 精确以 `ProtocolId + AiChannel.ImageGeneration` 查找；capability 必须声明 `image.generate` 和允许的模型/尺寸/响应格式。
- downloader/cache API 无 project path、没有任意保存目标，也没有从 image response 回调到 chat/profile resolver 的接口。

**测试**

- mock image API 覆盖 base64 与 URL response；验证 exact adapter payload、取消、超时、无 binding、wrong capability、unknown protocol、无效认证和 zero fallback。
- 受限 downloader 覆盖 HTTPS/loopback policy、重定向、cross-host credential stripping、大小上限、MIME/file-signature/decoder/pixel-limit、损坏 base64、恶意 URL、超限和取消；每一失败均不试图调用 chat 或第二 profile。
- cache 覆盖 SHA-256 去重、同卷原子写、并发、配额/LRU、损坏 entry、verified-root cleanup、无 `.tmp` 残留；所有 fixture 是合成图片，路径/文件名/日志中无 prompt、endpoint 或 secret。
- 静态测试拒绝对 `project/**`、Assets、Recipe、Patch 或任意用户选择路径的写入 API。

**门禁**

- image lane 对任意未解析/未验证 binding 的网络调用数为零；生成成功只能得到本地受控 cache artifact。
- 安全 downloader/cache 测试、redaction 测试与 Release build 全部通过；A3 变更与 A2 roots 完全不相交。
- 无自动项目导入、无自动图像上传、无 image-to-image/edit/mask 等未声明 capability。

**STOP**

不实现 Chat adapter；不把 provider URL 直接交给 UI；不写入 Unity 项目、资产或任何非 image-cache 目录。

### A4 — Desktop 双通道配置与功能 wiring

**目标**

把 A1–A3 已冻结的管理与运行时抽象接入 Desktop，提供清楚的两个 channel 选择、profile 编辑、secret 掩码、导入预览、capability 测试和 fail-closed 状态；业务功能仅获得 `IAiGateway`。

**计划拥有根**

- `apps/VFXComposer.Desktop/Views/SettingsView*`
- `apps/VFXComposer.Desktop/ViewModels/SettingsViewModel*`
- 仅为实际 Chat/Image feature wiring 所需的 Desktop ViewModel/服务文件
- `apps/VFXComposer.Desktop.Tests/**`
- A0 已批准后才可更新 Desktop project reference/solution entries

**API**

- 设置界面分为明确的“对话 / LLM API”和“图片 API”两个 binding 区，每区显示当前 profile、Origin、ProtocolId、capability/model、endpoint 的脱敏摘要、验证状态和“更换”动作。
- profile 编辑区把 Origin、Protocol、Endpoint、Auth、Capability 分开输入/确认；key 输入框只显示掩码。保留掩码/空编辑不会覆盖已有 key，明确“删除凭据”才删除 SecretRef。
- “导入 Tom JSON”只打开预览和映射报告；用户必须重新录入 credential、选择每条 channel binding、保存并测试。设置页只能调用 `IAiProviderSettingsAdmin`，功能 ViewModel 只能调用 `IAiGateway`。

**测试**

- Desktop/ViewModel tests 覆盖两个 binding 独立选择、一个 profile 双 capability 的显式双确认、未配置/验证失败显示、secret mask/clear、Tom 导入提示、channel mismatch、保存后 revision state。
- mock Gateway/admin tests 证明 UI 不持有 `HttpClient`/endpoint/token，不会把 secret/Payload 写入 diagnostic，且从 Chat feature 与 image feature 均只走对应 Gateway lane。
- 维持并扩展现有 Desktop “无 Unity 依赖/无项目访问 surface”测试：AI 配置和图片预览不得新增项目 I/O、Worker 连接或 Broker bypass。

**门禁**

- 两个独立通道的用户选择可见、可撤销、可测试、无 hidden default；用户无法仅凭 Origin 或 `Enabled` 让 profile 变成 active route。
- Desktop 通过所有 UI/静态边界测试，且没有 `System.Net.Http` direct-send/secret plaintext/new adapter 的 feature 路径。
- 人工可用性验收确认失败状态不会暗中尝试另一个 provider，也不会显示或复制密钥。

**STOP**

不把设置页变成网络旁路；不自动从当前 Unity 项目读 prompt/写生成物；不引入 Broker/Worker/Unity 改动；不以 UI 测试替代真实 mock E2E。

### A5 — CI mock end-to-end 与回归门禁

**目标**

在完全离线/可控 mock 环境中验证 Desktop 设置 -> 原子存储 -> explicit binding -> Gateway -> Chat/Image adapter -> image cache -> UI result 的完整链条，并把所有 fail-closed 与 redaction 要求固化到 CI。

**计划拥有根**

- `apps/VFXComposer.Desktop.Tests/**` 中经 A4 冻结的 mock E2E 范围，或 A0 认可的 `apps/VFXComposer.Desktop.Ai.E2E.Tests/**`
- `src/VFXComposer.AI.Providers.Tests/E2E/**`
- CI workflow/脚本中仅为该 mock suite 所需的精确文件
- A5 receipt/diagnostic 文档根（在 A5 启动时发布精确清单）

**API**

- test-only loopback/fake HTTP handler 与 synthetic image fixture；不新增 production listener、生产 endpoint override 或绕过 policy 的 test hook。
- E2E fixture 只能通过公开设置管理 API 填入测试 SecretRef，再经公开 Gateway API 调用；不能直接实例化 adapter 绕过 binding。

**测试**

- 成功路径：用户为 Chat 和 Image 分别选定 profile/capability；mock Chat 收到正确协议请求；mock Image 返回合成 base64/URL；图片只进入受控 cache；Desktop 只获得 artifact。
- 失败矩阵：无 binding、disabled profile、invalid secret、错误 protocol、origin/协议不匹配、401/429/5xx、bad JSON、bad image、redirect、cache disk failure、cancel、timeout、settings corruption、stale revision、断开/restart UI 状态。每项都断言没有 fallback、没有项目写入、没有 secret/raw payload 日志。
- import matrix：Tom metadata 的每种安全字段处理、密文不迁移、verification 不继承、用户重新输入 secret 后才可能测试成功。
- CI 以无网络权限/不可解析真实 host 的环境运行；扫描日志、测试输出、cache manifest 和 artifact 名称，确认不存在 key、Bearer、prompt、完整 URL、provider response 或真实图片。

**门禁**

- 受控 mock E2E、全量 unit/contract/UI tests、locked restore/build 和 `git diff --check` 均通过；CI 无真实 provider traffic/credential。
- 每一个 outbound production call site 都能由静态图和运行时 fake handler 证明先经过 Gateway，再经过 exact channel binding。
- A5 receipt 明确声明“mock E2E only”，不夸大为真实外部 Provider、订阅或图像服务验收。

**STOP**

不把 mock endpoint、test key、测试 bypass 或证书例外带入 release；不启动真实 Provider 自动验收；不让 cache 产物自动进入项目。

### A6 — 独立最终审计与后续发布判定

**目标**

对 A0–A5 冻结字节、依赖方向、配置安全、协议显式性、两通道隔离、网络/下载/cache 边界、日志脱敏和 CI receipts 进行独立只读审计。A6 不修代码。

**计划拥有根**

- 仅审计报告、证据索引、审计脚本/receipt 中在 A5 前已公布的精确根；任何源修改均为审计失败并须回到相应 A 节点处理。

**API**

- 无新增 production API。审计可以读取 frozen contract manifest、dependency graph、测试 receipt、redacted config fixtures 与 cache fixtures；不得读取用户真实设置或 secret。

**测试**

- 独立复跑 Contracts、Providers、Desktop 与 mock E2E gates；静态检查所有 outbound AI call sites、ProtocolId registry、no-fallback branches、origin/protocol separation、secret DTO leak、logging redaction、Broker/Worker/Unity/project exclusions。
- 审阅 atomic JSON crash/recovery 和 Tom import fixtures，确认没有密文迁移/解密、CLI/PATH/Cookie 探测或隐式旧配置激活。
- 审阅 image downloader/cache 的 URL/auth/redirect/content/cleanup 守卫和所有“不自动写项目”负面测试。

**门禁**

- 独立审计在声明范围内为 `P0=0 / P1=0 / P2=0`；所有 A0–A5 bytes、receipts、lock/project files和 source manifest 一致。
- 验证两个通道都只经 Gateway、每个调用都能解析到明确 binding/profile/capability/protocol/endpoint/auth，且任何无效状态都零 fallback。
- 发布结论仅说明本地 mock/组件范围；真实用户 Provider 配置可用性仍取决于用户选择的 endpoint、credential、额度和服务政策，不得作官方/中转/朋友/订阅服务的泛化承诺。

**STOP**

审计完成后停止。发现问题必须重新发布并回到对应 A0–A5 节点；A6 不能“顺手修复”源代码，不能扩大到 Broker/Worker/Unity、自动项目写入或真实外部服务验收。

## 7. 固定版本 Tom_doc 设计依据与有意差异

下表的链接全部固定到用户指定 commit `dd0f9ffc32d426735f7fb8960640e9b7ae9337bf`，避免把将来分支变化误当作本计划输入。

| Tom_doc 源 | 可借鉴的设计信号 | 本计划的有意差异 |
|---|---|---|
| [`README.md`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/README.md) | 明确列出官方 API、兼容/中转 API 与 DPAPI CurrentUser 的本地数据边界。 | 本产品将 AI 放在 U6 后的新链，且分为两个强制通道；不将 Provider 配置解释为 Unity/Broker authority。 |
| [`TomAiProviderModels.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomAiProviderModels.cs) | Provider type、endpoint、model、受保护 key、relay protocol、verification 与 request/response DTO 的分层起点。 | 不采用单一 `DefaultModel`/单一 enabled Provider 作为全局路由；改用 profile、protocol、auth、endpoint、capability、channel binding 六层分离和每 channel 一个显式选择。 |
| [`TomAiProviderAdapters.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomAiProviderAdapters.cs) | HTTP adapter interface、请求级 header、timeout/cancellation、不同协议的显式 payload/parser，以及官方订阅登录器的风险边界。 | Tom 对未知 type 回落到 OpenAI-compatible，并可用 sidecar CLI；本计划一律 fail closed，不启动 CLI/PATH/Cookie 登录，不让 unknown/relay 自动退回到任意 adapter。 |
| [`TomRelayProviderSupport.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomRelayProviderSupport.cs) | relay JSON 的字段提取、协议提示和导入预览是有价值的 UX 输入。 | Tom 的 `auto` detector 最终会默认尝试 OpenAI Chat；本计划只保留“草稿建议”，要求用户确认 ProtocolId，运行时禁止任何 URL/model/name 推断或 fallback。 |
| [`TomSecurityAndSettings.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomSecurityAndSettings.cs) | DPAPI CurrentUser、配置 revision/verification、settings store 与单一 enabled 规范化。 | 采用独立 purpose 的 DPAPI + SecretRef + 原子聚合设置；Tom `ApiKeyProtected` 密文不迁移，旧 verification 不信任，且两个 channel 的 binding 不能由 `Enabled` 隐式选择。 |
| [`TomAiSettingsForms.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomAiSettingsForms.cs) | 保存 key 的掩码、连接测试、relay 导入和字段按类型显示的配置 UX。 | Desktop 改为两个明确 binding 区，强制分开 Origin/Protocol/Endpoint/Auth/Capability；“保留掩码”与明确删除语义保留，但无隐藏默认路由。 |
| [`TomAiService.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomAiService.cs) | 在一个服务入口中装配 provider、adapter、解析和验证状态的思路。 | 本计划把入口强化为 channel-aware `IAiGateway`，业务请求不能指定 provider，Chat 与 Image 不共享 adapter、错误或 fallback。 |
| [`TomAtomicJsonFile.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomAtomicJsonFile.cs) | 同卷临时文件、flush、replace/move、最大长度和备份的原子 JSON 写入模式。 | 采用相同类别的原子性要求，但读取失败 fail closed 并保留恢复材料，不能静默生成空配置覆盖用户的 Provider/SecretRef 映射。 |
| [`TomHttpClients.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.App/TomHttpClients.cs) | 长寿命连接池、每请求认证 header、关闭 cookie 的网络卫生做法。 | 额外要求 Gateway-only 调用图、protocol/channel binding 解析、endpoint/TLS/redirect policy、无 raw payload 日志和 image 下载 credential stripping。 |
| [`Tom.MindMap.Tests/Program.cs`](https://github.com/snowpeak008/Tom_doc/blob/dd0f9ffc32d426735f7fb8960640e9b7ae9337bf/src/Tom.MindMap.Tests/Program.cs) | 对机器可读 schema、raw response 选择、timeout 与 atomic JSON 完整提交的测试意识。 | 新测试必须把无 fallback、双通道隔离、Tom 密文不迁移、secret/log 脱敏、图片 downloader/cache 和 CI mock E2E 设为硬 gate；不保存 raw request/response。 |

## 8. A0 启动前的 STOP 条件

A0 不能仅因为本文件存在而开始。以下任一项为真时保持停止：

- U6 尚未在 ADR-005 声明范围内完成独立 frozen-byte/evidence 审计；
- 没有为 A0 发布精确 owned-file manifest、分支、测试命令、审阅者和清理/回退规则；
- 任何提议把 AI 放入 Broker、Worker、Unity、项目文件系统、公开 listener 或自动写路径；
- 任何提议保留 Tom `ApiKeyProtected`、把 Origin 当协议、加入隐式 fallback、把测试 mock 当真实 Provider 验收，或把密钥/Prompt/原始响应写入日志；
- 用户尚未明确确认 post-U6 新链的范围、真实外部服务测试授权和可能新增的项目/包变更。

满足这些前提后，A0 仍只能按本文件的 gate 先做契约与边界冻结；不得跳过 A1 直接接入 Chat 或 Image Provider。
